using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PlanningScoreService : IPlanningScoreService
{
    private readonly IPlanningPolicyService _policyService;

    public PlanningScoreService(IPlanningPolicyService policyService)
    {
        _policyService = policyService;
    }

    public PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        return new PlanningCandidateAnalysis(ScoreCandidate(candidate, request), Array.Empty<string>(), Array.Empty<string>(), 0m);
    }

    public decimal GeoScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var scope = NormalizeScope(request.GeographyScope);
        var candidateCoverage = ResolveCandidateCoverage(candidate);

        var score = scope switch
        {
            "national" => candidateCoverage switch
            {
                "national" => 22m,
                "provincial" => 12m,
                "local" => 6m,
                _ => 8m
            },
            "provincial" => candidateCoverage switch
            {
                "provincial" => 22m,
                "local" => 14m,
                "national" => 10m,
                _ => 8m
            },
            "local" => candidateCoverage switch
            {
                "local" => 22m,
                "provincial" => 12m,
                "national" => 8m,
                _ => 8m
            },
            _ => 8m
        };

        if (request.Suburbs.Any(x => MatchesGeo(x, candidate.Suburb) || MatchesGeo(x, candidate.Area)))
        {
            score += 10m;
        }

        if (request.Cities.Any(x =>
            MatchesGeo(x, candidate.City)
            || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area")))
        {
            score += 10m;
        }

        if (request.Provinces.Any(x =>
            MatchesGeo(x, candidate.Province)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "province", "area")))
        {
            score += 10m;
        }

        if (request.Areas.Any(x => MatchesGeo(x, candidate.Area) || MatchesGeo(x, candidate.Suburb)))
        {
            score += 8m;
        }

        return Math.Min(36m, score);
    }

    public decimal AudienceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        if (request.TargetLanguages.Count > 0 && !string.IsNullOrWhiteSpace(candidate.Language))
        {
            if (request.TargetLanguages.Any(x => Matches(x, candidate.Language)))
            {
                score += 10m;
            }
        }

        if (request.TargetLsmMin.HasValue && request.TargetLsmMax.HasValue && candidate.LsmMin.HasValue && candidate.LsmMax.HasValue)
        {
            var overlap = !(candidate.LsmMax.Value < request.TargetLsmMin.Value || candidate.LsmMin.Value > request.TargetLsmMax.Value);
            if (overlap)
            {
                score += 15m;
            }
        }

        return score;
    }

    public decimal BudgetScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var comparableCost = GetComparableMonthlyCost(candidate);
        if (comparableCost <= 0 || request.SelectedBudget <= 0)
        {
            return 0m;
        }

        var ratio = comparableCost / request.SelectedBudget;

        if (ratio <= 0.15m) return 20m;
        if (ratio <= 0.30m) return 16m;
        if (ratio <= 0.50m) return 12m;
        if (ratio <= 0.80m) return 8m;
        if (ratio <= 1.00m) return 4m;
        return 0m;
    }

    public decimal MediaPreferenceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.PreferredMediaTypes.Count == 0) return 6m;
        return request.PreferredMediaTypes.Any(x => Matches(x, candidate.MediaType) || Matches(x, candidate.Subtype))
            ? 15m
            : 0m;
    }

    public decimal MixTargetScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var share = _policyService.GetTargetShare(candidate.MediaType, request);
        if (!share.HasValue) return 0m;
        if (share.Value <= 0) return -12m;
        if (share.Value >= 60) return 24m;
        if (share.Value >= 40) return 16m;
        if (share.Value >= 20) return 8m;
        return 3m;
    }

    private decimal ScoreCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        score += GeoScore(candidate, request);
        score += AudienceScore(candidate, request);
        score += BudgetScore(candidate, request);
        score += MediaPreferenceScore(candidate, request);
        score += AvailabilityScore(candidate);
        score += OohPriorityScore(candidate, request);
        score += MixTargetScore(candidate, request);

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            score += RadioFitBonus(candidate, request);
        }

        return score;
    }

    private decimal RadioFitBonus(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal bonus = 0m;

        if (!string.IsNullOrWhiteSpace(candidate.TimeBand))
        {
            bonus += Matches(candidate.TimeBand, "breakfast") || Matches(candidate.TimeBand, "drive") ? 8m : 4m;
        }

        if (!string.IsNullOrWhiteSpace(candidate.DayType) && Matches(candidate.DayType, "weekday"))
        {
            bonus += 3m;
        }

        if (!string.IsNullOrWhiteSpace(candidate.SlotType) && Matches(candidate.SlotType, "commercial"))
        {
            bonus += 4m;
        }

        bonus += _policyService.GetHigherBandRadioBonus(candidate, request);

        return bonus;
    }

    private static decimal AvailabilityScore(InventoryCandidate candidate) => candidate.IsAvailable ? 10m : 0m;

    private static decimal OohPriorityScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var preferredOoh = request.PreferredMediaTypes.Any(preferred =>
            Matches(preferred, "ooh") || Matches(preferred, candidate.MediaType) || Matches(preferred, candidate.Subtype));

        return preferredOoh ? 30m : 18m;
    }

    private static decimal GetComparableMonthlyCost(InventoryCandidate candidate)
    {
        if (candidate.Metadata.TryGetValue("monthlyCostEstimateZar", out var monthlyCost)
            && TryGetDecimal(monthlyCost, out var parsedMonthlyCost)
            && parsedMonthlyCost > 0m)
        {
            return parsedMonthlyCost;
        }

        if (candidate.Metadata.TryGetValue("monthly_cost_estimate_zar", out var snakeCaseMonthlyCost)
            && TryGetDecimal(snakeCaseMonthlyCost, out parsedMonthlyCost)
            && parsedMonthlyCost > 0m)
        {
            return parsedMonthlyCost;
        }

        return candidate.Cost;
    }

    private static bool TryGetDecimal(object? value, out decimal parsed)
    {
        switch (value)
        {
            case decimal decimalValue:
                parsed = decimalValue;
                return true;
            case double doubleValue:
                parsed = (decimal)doubleValue;
                return true;
            case float floatValue:
                parsed = (decimal)floatValue;
                return true;
            case int intValue:
                parsed = intValue;
                return true;
            case long longValue:
                parsed = longValue;
                return true;
            case string text when decimal.TryParse(text, out parsed):
                return true;
            case System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetDecimal(out parsed):
                return true;
            case System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(element.GetString(), out parsed):
                return true;
            default:
                parsed = 0m;
                return false;
        }
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = (scope ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "regional" => "provincial",
            "local" => "local",
            "provincial" => "provincial",
            "national" => "national",
            _ => normalized
        };
    }

    private static string ResolveCandidateCoverage(InventoryCandidate candidate)
    {
        var scope = (candidate.MarketScope ?? string.Empty).Trim().ToLowerInvariant();
        if (scope.Contains("national", StringComparison.OrdinalIgnoreCase))
        {
            return "national";
        }

        if (scope.Contains("provincial", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("regional", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("province", StringComparison.OrdinalIgnoreCase))
        {
            return "provincial";
        }

        if (scope.Contains("local", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("city", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("suburb", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }

        if (!string.IsNullOrWhiteSpace(candidate.City) || !string.IsNullOrWhiteSpace(candidate.Suburb))
        {
            return "local";
        }

        if (!string.IsNullOrWhiteSpace(candidate.Province) || !string.IsNullOrWhiteSpace(candidate.RegionClusterCode))
        {
            return "provincial";
        }

        return "unknown";
    }

    private static bool MatchesGeo(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return NormalizeGeoToken(left) == NormalizeGeoToken(right);
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeGeoToken(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        return normalized switch
        {
            "zaec" or "ec" => "easterncape",
            "zafs" or "fs" => "freestate",
            "zagt" or "gt" => "gauteng",
            "zakzn" or "kzn" => "kwazulunatal",
            "zalp" or "lp" => "limpopo",
            "zamp" or "mp" => "mpumalanga",
            "zanc" or "nc" => "northerncape",
            "zanw" or "nw" => "northwest",
            "zawc" or "wc" => "westerncape",
            _ => normalized
        };
    }

    private static bool MatchesAnyMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(requestedValue) || candidate.Metadata.Count == 0)
        {
            return false;
        }

        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => MatchesGeo(requestedValue, token)));
    }

    private static IEnumerable<string> ExtractMetadataTokens(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text.Trim();
            }

            yield break;
        }

        if (value is IEnumerable<string> textValues)
        {
            foreach (var entry in textValues)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    yield return entry.Trim();
                }
            }

            yield break;
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                var jsonText = json.GetString();
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    yield return jsonText.Trim();
                }

                yield break;
            }

            if (json.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in json.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var itemText = item.GetString();
                        if (!string.IsNullOrWhiteSpace(itemText))
                        {
                            yield return itemText.Trim();
                        }
                    }
                }
            }

            yield break;
        }

        var fallback = value.ToString();
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            yield return fallback.Trim();
        }
    }
}

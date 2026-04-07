using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PlanningScoreService : IPlanningScoreService
{
    private const decimal BroadcastPrimaryLanguageMatchScore = 32m;
    private const decimal BroadcastSecondaryLanguageMatchScore = 20m;
    private const decimal BroadcastLanguageMismatchPenalty = -24m;
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
        score += LanguageScore(candidate, request);

        if (request.TargetLsmMin.HasValue && request.TargetLsmMax.HasValue && candidate.LsmMin.HasValue && candidate.LsmMax.HasValue)
        {
            var overlap = !(candidate.LsmMax.Value < request.TargetLsmMin.Value || candidate.LsmMin.Value > request.TargetLsmMax.Value);
            if (overlap)
            {
                score += 15m;
            }
        }

        score += AgeScore(candidate, request);
        score += GenderScore(candidate, request);
        score += AudienceKeywordScore(candidate, request);

        return score;
    }

    private static decimal LanguageScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.TargetLanguages.Count == 0)
        {
            return 0m;
        }

        var requested = request.TargetLanguages
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeLanguage)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 0)
        {
            return 0m;
        }

        var hasPrimaryLanguageMatch = requested.Any(value =>
            MatchesAnyMetadataToken(candidate, value, "primaryLanguages", "primary_languages", "language"));

        var hasLanguageNotesMatch = requested.Any(value =>
            MatchesAnyMetadataToken(candidate, value, "languageNotes", "language_notes", "targetAudience", "target_audience"));

        var hasCandidateLanguageMatch = !string.IsNullOrWhiteSpace(candidate.Language)
            && requested.Any(value => MatchesLanguage(value, candidate.Language));

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
            || candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase))
        {
            if (hasPrimaryLanguageMatch)
            {
                return BroadcastPrimaryLanguageMatchScore;
            }

            if (hasCandidateLanguageMatch || hasLanguageNotesMatch)
            {
                return BroadcastSecondaryLanguageMatchScore;
            }

            return BroadcastLanguageMismatchPenalty;
        }

        if (hasPrimaryLanguageMatch || hasCandidateLanguageMatch)
        {
            return 10m;
        }

        return hasLanguageNotesMatch ? 6m : 0m;
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
        score += ObjectiveFitScore(candidate, request);
        score += StrategyFitScore(candidate, request);
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

    private static decimal StrategyFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var signals = CampaignStrategySupport.BuildSignals(request);
        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        decimal score = 0m;

        if (signals.PremiumAudience)
        {
            if (mediaType is "ooh" or "tv")
            {
                score += 4m;
            }

            if (HasLsmOverlap(candidate, 7, 10))
            {
                score += 6m;
            }
        }

        if (signals.MassMarketAudience)
        {
            if (mediaType is "radio" or "ooh")
            {
                score += 4m;
            }

            if (HasLsmOverlap(candidate, 4, 7))
            {
                score += 5m;
            }
        }

        if (signals.FastDecisionCycle || signals.ImmediateUrgency)
        {
            score += mediaType switch
            {
                "radio" => 6m,
                "ooh" => 5m,
                "digital" => 5m,
                "tv" => 1m,
                _ => 0m
            };
        }
        else if (signals.LongDecisionCycle)
        {
            score += mediaType switch
            {
                "tv" => 5m,
                "ooh" => 4m,
                "radio" => 3m,
                "digital" => 3m,
                _ => 0m
            };
        }

        if (signals.WalkInDriven)
        {
            score += mediaType switch
            {
                "ooh" => 6m,
                "radio" => 4m,
                _ => 0m
            };
        }

        if (signals.OnlineDriven)
        {
            score += mediaType switch
            {
                "digital" => 8m,
                "radio" => 2m,
                "ooh" => -1m,
                _ => 0m
            };
        }

        if (signals.AudienceClearlyDefined && HasAudienceMetadata(candidate))
        {
            score += 3m;
        }

        if (signals.AudienceNeedsBroadReach)
        {
            if (mediaType is "ooh" or "tv")
            {
                score += 4m;
            }

            if (ResolveCandidateCoverage(candidate) is "national" or "provincial")
            {
                score += 2m;
            }
        }

        if (signals.HighGrowthAmbition && mediaType is "ooh" or "radio" or "tv")
        {
            score += 2m;
        }

        if (signals.EnterpriseOrGovernment && mediaType == "radio")
        {
            score += 3m;
        }

        if (MatchesStrategyMetadata(candidate, request.BuyingBehaviour, "buyingBehaviourFit", "buying_behaviour_fit"))
        {
            score += 5m;
        }

        if (MatchesStrategyMetadata(candidate, request.PricePositioning, "pricePositioningFit", "price_positioning_fit"))
        {
            score += 5m;
        }

        if (MatchesStrategyMetadata(candidate, request.SalesModel, "salesModelFit", "sales_model_fit"))
        {
            score += 6m;
        }

        if (signals.PremiumAudience && MatchesMetadataToken(candidate, "premium", "premiumMassFit", "premium_mass_fit"))
        {
            score += 5m;
        }

        if (signals.MassMarketAudience && MatchesMetadataToken(candidate, "mass_market", "premiumMassFit", "premium_mass_fit"))
        {
            score += 5m;
        }

        return Math.Min(22m, Math.Max(-4m, score));
    }

    private static decimal AgeScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!request.TargetAgeMin.HasValue && !request.TargetAgeMax.HasValue)
        {
            return 0m;
        }

        var ageText = GetMetadataText(candidate, "audienceAgeSkew", "audience_age_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(ageText))
        {
            return 0m;
        }

        var requestedMin = request.TargetAgeMin ?? request.TargetAgeMax ?? 13;
        var requestedMax = request.TargetAgeMax ?? request.TargetAgeMin ?? 100;
        if (TryParseAgeRange(ageText, out var candidateMin, out var candidateMax))
        {
            var overlap = !(candidateMax < requestedMin || candidateMin > requestedMax);
            return overlap ? 8m : 0m;
        }

        var requestedTokens = BuildAgeTokens(requestedMin, requestedMax);
        return requestedTokens.Any(token => ageText.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? 5m
            : 0m;
    }

    private static decimal GenderScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var targetGender = NormalizeGender(request.TargetGender);
        if (string.IsNullOrWhiteSpace(targetGender) || targetGender == "all")
        {
            return 0m;
        }

        var genderText = GetMetadataText(candidate, "audienceGenderSkew", "audience_gender_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(genderText))
        {
            return 0m;
        }

        return GenderAliases(targetGender).Any(alias => genderText.Contains(alias, StringComparison.OrdinalIgnoreCase))
            ? 5m
            : 0m;
    }

    private static decimal AudienceKeywordScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var candidateText = BuildAudienceSearchText(candidate);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return 0m;
        }

        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var interest in request.TargetInterests)
        {
            foreach (var token in TokenizeAudienceTerms(interest))
            {
                if (candidateText.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(token);
                }
            }
        }

        foreach (var token in TokenizeAudienceTerms(request.TargetAudienceNotes).Take(8))
        {
            if (candidateText.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(token);
            }
        }

        return matches.Count switch
        {
            >= 3 => 8m,
            2 => 6m,
            1 => 4m,
            _ => 0m
        };
    }

    private static decimal ObjectiveFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var objective = (request.Objective ?? string.Empty).Trim().ToLowerInvariant();
        if (objective.Length == 0)
        {
            return 0m;
        }

        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        var score = objective switch
        {
            "awareness" or "brand_presence" => mediaType switch
            {
                "ooh" => 10m,
                "tv" => 9m,
                "radio" => 7m,
                "digital" => 6m,
                _ => 0m
            },
            "launch" => mediaType switch
            {
                "ooh" => 10m,
                "radio" => 8m,
                "tv" => 8m,
                "digital" => 6m,
                _ => 0m
            },
            "promotion" => mediaType switch
            {
                "radio" => 9m,
                "ooh" => 8m,
                "digital" => 7m,
                "tv" => 4m,
                _ => 0m
            },
            "leads" => mediaType switch
            {
                "digital" => 10m,
                "radio" => 8m,
                "ooh" => 3m,
                "tv" => 2m,
                _ => 0m
            },
            "foot_traffic" => mediaType switch
            {
                "ooh" => 10m,
                "radio" => 8m,
                "digital" => 5m,
                "tv" => 2m,
                _ => 0m
            },
            _ => 0m
        };

        if (mediaType == "radio" && (!string.IsNullOrWhiteSpace(candidate.TimeBand) || !string.IsNullOrWhiteSpace(candidate.DayType)))
        {
            if (objective is "leads" or "promotion" or "foot_traffic")
            {
                if (Matches(candidate.TimeBand, "breakfast") || Matches(candidate.TimeBand, "drive"))
                {
                    score += 2m;
                }

                if (Matches(candidate.DayType, "weekday"))
                {
                    score += 1m;
                }
            }
        }

        if (mediaType == "ooh" && objective is "awareness" or "brand_presence" or "launch" or "foot_traffic")
        {
            if (!string.IsNullOrWhiteSpace(candidate.Area) || !string.IsNullOrWhiteSpace(candidate.City))
            {
                score += 1m;
            }
        }

        if (MatchesMetadataToken(candidate, objective, "objectiveFitPrimary", "objective_fit_primary"))
        {
            score += 8m;
        }
        else if (MatchesMetadataToken(candidate, objective, "objectiveFitSecondary", "objective_fit_secondary"))
        {
            score += 4m;
        }

        return score;
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

    private static bool HasLsmOverlap(InventoryCandidate candidate, int requestMin, int requestMax)
    {
        if (candidate.LsmMin.HasValue && candidate.LsmMax.HasValue)
        {
            return !(candidate.LsmMax.Value < requestMin || candidate.LsmMin.Value > requestMax);
        }

        var lsmText = GetMetadataText(candidate, "audienceLsmRange", "audience_lsm_range", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(lsmText))
        {
            return false;
        }

        var values = Regex.Matches(lsmText, "\\d+")
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (values.Length >= 2)
        {
            var candidateMin = Math.Min(values[0], values[1]);
            var candidateMax = Math.Max(values[0], values[1]);
            return !(candidateMax < requestMin || candidateMin > requestMax);
        }

        return false;
    }

    private static bool HasAudienceMetadata(InventoryCandidate candidate)
    {
        return !string.IsNullOrWhiteSpace(GetMetadataText(
            candidate,
            "audienceKeywords",
            "audience_keywords",
            "targetAudience",
            "target_audience",
            "audienceAgeSkew",
            "audience_age_skew",
            "audienceGenderSkew",
            "audience_gender_skew",
            "buyingBehaviourFit",
            "buying_behaviour_fit",
            "pricePositioningFit",
            "price_positioning_fit",
            "salesModelFit",
            "sales_model_fit"));
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
            && ExtractMetadataTokens(value).Any(token => MatchesGeo(requestedValue, token) || MatchesLanguage(requestedValue, token)));
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

    private static string GetMetadataText(InventoryCandidate candidate, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (candidate.Metadata.TryGetValue(key, out var value))
            {
                var flattened = string.Join(" ", ExtractMetadataTokens(value));
                if (!string.IsNullOrWhiteSpace(flattened))
                {
                    return flattened;
                }
            }
        }

        return string.Empty;
    }

    private static string BuildAudienceSearchText(InventoryCandidate candidate)
    {
        var parts = new[]
            {
                candidate.DisplayName,
                candidate.MediaType,
                candidate.Subtype,
                candidate.Language,
                GetMetadataText(candidate, "targetAudience", "target_audience", "notes", "packageName", "package_name", "audienceAgeSkew", "audience_age_skew", "audienceGenderSkew", "audience_gender_skew", "environmentType", "environment_type", "inventoryIntelligenceNotes", "inventory_intelligence_notes")
            }
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToList()!;

        foreach (var key in new[] { "audienceKeywords", "audience_keywords", "keywords" })
        {
            if (candidate.Metadata.TryGetValue(key, out var value))
            {
                parts.AddRange(ExtractMetadataTokens(value));
            }
        }

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static bool MatchesStrategyMetadata(InventoryCandidate candidate, string? requestedValue, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        return MatchesMetadataToken(candidate, requestedValue, keys);
    }

    private static bool MatchesMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => MatchesStrategyToken(requestedValue, token)));
    }

    private static bool MatchesStrategyToken(string requestedValue, string metadataToken)
    {
        var normalizedRequested = NormalizeStrategyToken(requestedValue);
        var normalizedMetadata = NormalizeStrategyToken(metadataToken);
        if (normalizedRequested.Length == 0 || normalizedMetadata.Length == 0)
        {
            return false;
        }

        return normalizedRequested == normalizedMetadata
            || normalizedMetadata.Contains(normalizedRequested, StringComparison.OrdinalIgnoreCase)
            || normalizedRequested.Contains(normalizedMetadata, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStrategyToken(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace('|', ' ')
            .Replace('/', ' ')
            .Replace('-', '_')
            .Replace(' ', '_');
    }

    private static IEnumerable<string> TokenizeAudienceTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text, "[A-Za-z]{4,}"))
        {
            var token = match.Value.Trim().ToLowerInvariant();
            if (token is "that" or "this" or "with" or "from" or "your" or "their" or "have" or "into" or "across" or "need" or "needs" or "market" or "audience" or "customer" or "customers")
            {
                continue;
            }

            if (seen.Add(token))
            {
                yield return token;
            }
        }
    }

    private static bool TryParseAgeRange(string text, out int min, out int max)
    {
        min = 0;
        max = 0;

        var normalized = text.Trim().ToLowerInvariant();
        var values = Regex.Matches(normalized, "\\d+")
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (normalized.Contains('+') && values.Length >= 1)
        {
            min = values[0];
            max = 100;
            return true;
        }

        if (values.Length >= 2)
        {
            min = Math.Min(values[0], values[1]);
            max = Math.Max(values[0], values[1]);
            return true;
        }

        return normalized switch
        {
            var value when value.Contains("youth", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(15, 24, out min, out max),
            var value when value.Contains("young", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(18, 34, out min, out max),
            var value when value.Contains("adult", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(25, 54, out min, out max),
            var value when value.Contains("family", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(25, 54, out min, out max),
            _ => false
        };
    }

    private static bool AssignAgeRange(int rangeMin, int rangeMax, out int min, out int max)
    {
        min = rangeMin;
        max = rangeMax;
        return true;
    }

    private static IEnumerable<string> BuildAgeTokens(int min, int max)
    {
        if (max <= 24)
        {
            yield return "youth";
        }

        if (min <= 34 && max >= 18)
        {
            yield return "young";
        }

        if (max >= 25)
        {
            yield return "adult";
        }
    }

    private static string NormalizeGender(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "male" or "man" or "men" => "male",
            "female" or "woman" or "women" => "female",
            "all" or "everyone" or "mixed" => "all",
            _ => string.Empty
        };
    }

    private static bool MatchesLanguage(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(NormalizeLanguage(left), NormalizeLanguage(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguage(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        return normalized switch
        {
            "english" => "english",
            "isizulu" or "zulu" => "zulu",
            "isixhosa" or "xhosa" => "xhosa",
            "afrikaans" => "afrikaans",
            "sesotho" or "sotho" => "sotho",
            "setswana" or "tswana" => "tswana",
            "sepedi" or "pedi" => "pedi",
            "xitsonga" or "itsonga" => "xitsonga",
            "tshivenda" or "venda" => "venda",
            "siswati" or "swati" => "swati",
            "isindebele" or "ndebele" => "ndebele",
            "multilingual" or "multi" => "multilingual",
            _ => normalized
        };
    }

    private static IEnumerable<string> GenderAliases(string normalizedGender)
    {
        return normalizedGender switch
        {
            "male" => new[] { "male", "men", "man", "guy", "gent" },
            "female" => new[] { "female", "women", "woman", "lady" },
            _ => Array.Empty<string>()
        };
    }
}

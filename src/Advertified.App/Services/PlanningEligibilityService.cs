using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PlanningEligibilityService : IPlanningEligibilityService
{
    private readonly IPlanningPolicyService _policyService;

    public PlanningEligibilityService(IPlanningPolicyService policyService)
    {
        _policyService = policyService;
    }

    public PlanningPolicyOutcome FilterEligibleCandidates(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        var eligible = candidates
            .Where(candidate => IsEligibleCandidate(candidate, request))
            .ToList();

        return _policyService.ApplyHigherBandRadioEligibility(eligible, request);
    }

    private static bool IsEligibleCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.IsAvailable)
        {
            return false;
        }

        if (candidate.Cost <= 0 || candidate.Cost > request.SelectedBudget)
        {
            return false;
        }

        if (request.ExcludedMediaTypes.Contains(candidate.MediaType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesRequestedGeography(candidate, request);
    }

    private static bool MatchesRequestedGeography(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var normalizedScope = NormalizeScope(request.GeographyScope);
        if (normalizedScope == "national")
        {
            return true;
        }

        // National broadcast inventory (radio + TV) should remain eligible across
        // local/provincial briefs. This prevents false preferred-media fallbacks when
        // channels are inherently national even if the brief geography is narrower.
        if ((candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
                || candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase))
            && Matches(candidate.MarketScope, "national"))
        {
            return true;
        }

        var requestedSuburbs = normalizedScope == "local" ? request.Suburbs : new List<string>();
        var requestedCities = normalizedScope == "local" ? request.Cities : new List<string>();
        var requestedProvinces = normalizedScope == "provincial" ? request.Provinces : new List<string>();
        var requestedAreas = normalizedScope == "local" ? request.Areas : new List<string>();

        var hasSpecificGeography = requestedSuburbs.Count() > 0
            || requestedCities.Count() > 0
            || requestedProvinces.Count() > 0
            || requestedAreas.Count() > 0;

        if (!hasSpecificGeography)
        {
            return true;
        }

        if (requestedSuburbs.Any(x => Matches(x, candidate.Suburb) || Matches(x, candidate.Area)))
        {
            return true;
        }

        if (requestedCities.Any(x =>
            Matches(x, candidate.City)
            || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area")))
        {
            return true;
        }

        if (requestedProvinces.Any(x =>
            Matches(x, candidate.Province)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "province", "area")))
        {
            return true;
        }

        if (requestedAreas.Any(x =>
            Matches(x, candidate.Area)
            || Matches(x, candidate.Suburb)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "cityLabels", "city_labels", "area", "province", "city")))
        {
            return true;
        }

        return Matches(normalizedScope, candidate.MarketScope)
            || Matches(normalizedScope, candidate.RegionClusterCode);
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedLeft = NormalizeGeoToken(left);
        var normalizedRight = NormalizeGeoToken(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        return normalizedLeft == normalizedRight;
    }

    private static bool MatchesAnyMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(requestedValue) || candidate.Metadata.Count == 0)
        {
            return false;
        }

        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => Matches(requestedValue, token)));
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
}

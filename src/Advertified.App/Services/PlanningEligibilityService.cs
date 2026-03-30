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

        if (!MatchesPreferredMedia(candidate, request))
        {
            return false;
        }

        return MatchesRequestedGeography(candidate, request);
    }

    private static bool MatchesPreferredMedia(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.PreferredMediaTypes.Count == 0)
        {
            return true;
        }

        return request.PreferredMediaTypes.Any(preferred =>
            Matches(preferred, candidate.MediaType)
            || Matches(preferred, candidate.Subtype));
    }

    private static bool MatchesRequestedGeography(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var hasSpecificGeography = request.Suburbs.Count > 0
            || request.Cities.Count > 0
            || request.Provinces.Count > 0
            || request.Areas.Count > 0;

        if (!hasSpecificGeography)
        {
            return true;
        }

        if (request.Suburbs.Any(x => Matches(x, candidate.Suburb) || Matches(x, candidate.Area)))
        {
            return true;
        }

        if (request.Cities.Any(x =>
            Matches(x, candidate.City)
            || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area")))
        {
            return true;
        }

        if (request.Provinces.Any(x =>
            Matches(x, candidate.Province)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "province", "area")))
        {
            return true;
        }

        if (request.Areas.Any(x =>
            Matches(x, candidate.Area)
            || Matches(x, candidate.Suburb)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "cityLabels", "city_labels", "area", "province", "city")))
        {
            return true;
        }

        return Matches(request.GeographyScope, candidate.MarketScope)
            || Matches(request.GeographyScope, candidate.RegionClusterCode);
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
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
}

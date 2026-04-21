using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PlanningEligibilityService : IPlanningEligibilityService
{
    private const double LocalSuburbRadiusKm = 30.0;
    private readonly IPlanningPolicyService _policyService;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;
    private readonly IPlanningBriefIntentService _briefIntentService;

    public PlanningEligibilityService(
        IPlanningPolicyService policyService,
        IBroadcastMasterDataService broadcastMasterDataService,
        IPlanningBriefIntentService briefIntentService)
    {
        _policyService = policyService;
        _broadcastMasterDataService = broadcastMasterDataService;
        _briefIntentService = briefIntentService;
    }

    public PlanningEligibilityService(IPlanningPolicyService policyService, IBroadcastMasterDataService broadcastMasterDataService)
        : this(policyService, broadcastMasterDataService, new NoOpPlanningBriefIntentService())
    {
    }

    public PlanningPolicyOutcome FilterEligibleCandidates(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        var eligible = new List<InventoryCandidate>(candidates.Count);
        var rejections = new List<PlanningCandidateRejection>();

        foreach (var candidate in candidates)
        {
            var rejectionReason = GetEligibilityRejectionReason(candidate, request);
            if (rejectionReason is null)
            {
                eligible.Add(candidate);
                continue;
            }

            rejections.Add(new PlanningCandidateRejection(
                "eligibility",
                rejectionReason,
                candidate.SourceId,
                candidate.DisplayName,
                NormalizeMediaType(candidate.MediaType)));
        }

        var policyOutcome = _policyService.ApplyHigherBandRadioEligibility(eligible, request);
        return new PlanningPolicyOutcome(
            policyOutcome.Candidates,
            policyOutcome.FallbackFlags,
            rejections.Concat(policyOutcome.Rejections).ToList());
    }

    private string? GetEligibilityRejectionReason(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.IsAvailable)
        {
            return "candidate_unavailable";
        }

        if (candidate.Cost <= 0 || candidate.Cost > request.SelectedBudget)
        {
            return "cost_out_of_budget";
        }

        if (request.ExcludedMediaTypes.Contains(candidate.MediaType, StringComparer.OrdinalIgnoreCase))
        {
            return "media_type_excluded";
        }

        if (MatchesExcludedArea(candidate, request))
        {
            return "excluded_area";
        }

        if (!MatchesRequestedGeography(candidate, request))
        {
            return "geography_mismatch";
        }

        var briefIntentEvaluation = _briefIntentService.EvaluateCandidate(candidate, request);
        if (!briefIntentEvaluation.IsEligible)
        {
            return "brief_intent_mismatch";
        }

        return null;
    }

    private bool MatchesRequestedGeography(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var normalizedScope = NormalizeScope(request.GeographyScope);
        if (normalizedScope == "national")
        {
            return true;
        }

        var normalizedMediaType = NormalizeMediaType(candidate.MediaType);

        var isBroadcast = normalizedMediaType is "radio" or "tv";
        var isOohLike = normalizedMediaType is "ooh" or "billboard" or "digital_screen" or "digital";

        // National broadcast inventory (radio + TV) and national digital packages
        // should remain eligible across local/provincial briefs. This prevents false
        // preferred-media fallbacks when channels are inherently national even if the
        // brief geography is narrower.
        if ((isBroadcast || normalizedMediaType == "digital")
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

        var matchesRequestedCity = requestedCities.Count == 0
            || requestedCities.Any(x =>
                Matches(x, candidate.City)
                || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area"));

        if (normalizedScope == "local"
            && requestedCities.Count > 0
            && isOohLike
            && request.TargetLatitude.HasValue
            && request.TargetLongitude.HasValue
            && candidate.Latitude.HasValue
            && candidate.Longitude.HasValue)
        {
            var distanceKm = HaversineDistanceKm(
                request.TargetLatitude.Value,
                request.TargetLongitude.Value,
                candidate.Latitude.Value,
                candidate.Longitude.Value);

            if (distanceKm <= LocalSuburbRadiusKm)
            {
                return true;
            }
        }

        if (requestedSuburbs.Count > 0)
        {
            // Suburb targeting is intended for hyperlocal surfaces (OOH/digital). Broadcast inventory
            // is not sold at suburb precision, so we keep it eligible based on city/province matching.
            if (isBroadcast)
            {
                return matchesRequestedCity
                    || Matches(normalizedScope, candidate.MarketScope)
                    || Matches(normalizedScope, candidate.RegionClusterCode);
            }

            // If we have a geocoded campaign target and the inventory has coordinates, use radius-based matching.
            if (isOohLike
                && request.TargetLatitude.HasValue
                && request.TargetLongitude.HasValue
                && candidate.Latitude.HasValue
                && candidate.Longitude.HasValue)
            {
                var distanceKm = HaversineDistanceKm(
                    request.TargetLatitude.Value,
                    request.TargetLongitude.Value,
                    candidate.Latitude.Value,
                    candidate.Longitude.Value);

                if (distanceKm <= LocalSuburbRadiusKm)
                {
                    return true;
                }
            }

            var matchesSuburb = requestedSuburbs.Any(x => Matches(x, candidate.Suburb) || Matches(x, candidate.Area));
            return matchesRequestedCity && matchesSuburb;
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
            return matchesRequestedCity;
        }

        return Matches(normalizedScope, candidate.MarketScope)
            || Matches(normalizedScope, candidate.RegionClusterCode);
    }

    private bool MatchesExcludedArea(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.ExcludedAreas.Count == 0)
        {
            return false;
        }

        return request.ExcludedAreas.Any(area =>
            Matches(area, candidate.Area)
            || Matches(area, candidate.Suburb)
            || Matches(area, candidate.City)
            || MatchesAnyMetadataToken(candidate, area, "cityLabels", "city_labels", "city", "area", "provinceCodes", "province_codes", "province"));
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radiusKm = 6371.0;
        static double ToRadians(double angle) => Math.PI * angle / 180.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return radiusKm * c;
    }

    private bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedLeft = _broadcastMasterDataService.NormalizeGeographyForMatching(left);
        var normalizedRight = _broadcastMasterDataService.NormalizeGeographyForMatching(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        return normalizedLeft == normalizedRight;
    }

    private bool MatchesAnyMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
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

    private static string NormalizeMediaType(string? mediaType)
    {
        return string.IsNullOrWhiteSpace(mediaType)
            ? "unknown"
            : mediaType.Trim().ToLowerInvariant();
    }

    private sealed class NoOpPlanningBriefIntentService : IPlanningBriefIntentService
    {
        public PlanningBriefIntentEvaluation EvaluateCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
        {
            return new PlanningBriefIntentEvaluation();
        }
    }
}

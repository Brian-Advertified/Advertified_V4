using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
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

        if (request.ExcludedMediaTypes.Any(excluded =>
                PlanningChannelSupport.MatchesRequestedChannel(candidate.MediaType, excluded)))
        {
            return "media_type_excluded";
        }

        if (MatchesExcludedArea(candidate, request))
        {
            return "excluded_area";
        }

        if (!EvaluateGeography(candidate, request).IsMatch)
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

    public PlanningGeographyEvaluation EvaluateGeography(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var normalizedScope = NormalizeScope(request.GeographyScope);
        var coverage = ResolveCandidateCoverage(candidate);
        var matchesPriorityArea = MatchesPriorityArea(candidate, request);
        var matchesBusinessOrigin = MatchesBusinessOrigin(candidate, request);
        if (normalizedScope == "national")
        {
            return new PlanningGeographyEvaluation(
                true,
                normalizedScope,
                coverage,
                MatchesSuburb: false,
                MatchesCity: false,
                MatchesProvince: false,
                MatchesArea: false,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: false);
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
            return new PlanningGeographyEvaluation(
                true,
                normalizedScope,
                coverage,
                MatchesSuburb: false,
                MatchesCity: false,
                MatchesProvince: false,
                MatchesArea: false,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: true);
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
            return new PlanningGeographyEvaluation(
                true,
                normalizedScope,
                coverage,
                MatchesSuburb: false,
                MatchesCity: false,
                MatchesProvince: false,
                MatchesArea: false,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: false);
        }

        var matchesRequestedCity = requestedCities.Count == 0
            || requestedCities.Any(x =>
                Matches(x, candidate.City)
                || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area"));
        var explicitCityMatch = requestedCities.Count > 0 && requestedCities.Any(x =>
            Matches(x, candidate.City)
            || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area"));
        var matchesRequestedProvince = requestedProvinces.Any(x => MatchesRequestedProvince(candidate, x, isBroadcast));
        var matchesRequestedArea = requestedAreas.Any(x =>
            Matches(x, candidate.Area)
            || Matches(x, candidate.Suburb)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "cityLabels", "city_labels", "area", "province", "city"));
        var explicitSuburbMatch = requestedSuburbs.Any(x => Matches(x, candidate.Suburb) || Matches(x, candidate.Area));

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
                return new PlanningGeographyEvaluation(
                    true,
                    normalizedScope,
                    coverage,
                    MatchesSuburb: requestedSuburbs.Count > 0,
                    MatchesCity: explicitCityMatch,
                    MatchesProvince: matchesRequestedProvince,
                    MatchesArea: matchesRequestedArea,
                    matchesPriorityArea,
                    matchesBusinessOrigin,
                    UsedRadiusMatch: true,
                    UsedNationalInventoryOverride: false);
            }
        }

        if (requestedSuburbs.Count > 0)
        {
            // Suburb targeting is intended for hyperlocal surfaces (OOH/digital). Broadcast inventory
            // is not sold at suburb precision, so we keep it eligible based on city/province matching.
            if (isBroadcast)
            {
                return new PlanningGeographyEvaluation(
                    matchesRequestedCity,
                    normalizedScope,
                    coverage,
                    MatchesSuburb: false,
                    MatchesCity: explicitCityMatch,
                    MatchesProvince: matchesRequestedProvince,
                    MatchesArea: matchesRequestedArea,
                    matchesPriorityArea,
                    matchesBusinessOrigin,
                    UsedRadiusMatch: false,
                    UsedNationalInventoryOverride: false);
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
                    return new PlanningGeographyEvaluation(
                        true,
                        normalizedScope,
                        coverage,
                        MatchesSuburb: true,
                        MatchesCity: explicitCityMatch,
                        MatchesProvince: matchesRequestedProvince,
                        MatchesArea: matchesRequestedArea,
                        matchesPriorityArea,
                        matchesBusinessOrigin,
                        UsedRadiusMatch: true,
                        UsedNationalInventoryOverride: false);
                }
            }

            return new PlanningGeographyEvaluation(
                matchesRequestedCity && explicitSuburbMatch,
                normalizedScope,
                coverage,
                explicitSuburbMatch,
                explicitCityMatch,
                matchesRequestedProvince,
                matchesRequestedArea,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: false);
        }

        if (explicitCityMatch)
        {
            return new PlanningGeographyEvaluation(
                true,
                normalizedScope,
                coverage,
                MatchesSuburb: false,
                MatchesCity: true,
                MatchesProvince: matchesRequestedProvince,
                MatchesArea: matchesRequestedArea,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: false);
        }

        if (matchesRequestedProvince)
        {
            return new PlanningGeographyEvaluation(
                true,
                normalizedScope,
                coverage,
                MatchesSuburb: false,
                MatchesCity: false,
                MatchesProvince: true,
                MatchesArea: matchesRequestedArea,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: false);
        }

        if (matchesRequestedArea)
        {
            return new PlanningGeographyEvaluation(
                matchesRequestedCity,
                normalizedScope,
                coverage,
                MatchesSuburb: false,
                MatchesCity: explicitCityMatch,
                MatchesProvince: matchesRequestedProvince,
                MatchesArea: true,
                matchesPriorityArea,
                matchesBusinessOrigin,
                UsedRadiusMatch: false,
                UsedNationalInventoryOverride: false);
        }

        return new PlanningGeographyEvaluation(
            false,
            normalizedScope,
            coverage,
            MatchesSuburb: false,
            MatchesCity: explicitCityMatch,
            MatchesProvince: matchesRequestedProvince,
            MatchesArea: matchesRequestedArea,
            matchesPriorityArea,
            matchesBusinessOrigin,
            UsedRadiusMatch: false,
            UsedNationalInventoryOverride: false);
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

        return PlanningMetadataSupport.MatchesAnyMetadataToken(candidate, token => Matches(requestedValue, token), keys);
    }

    private bool MatchesRequestedProvince(InventoryCandidate candidate, string requestedProvince, bool isBroadcast)
    {
        if (!isBroadcast)
        {
            return Matches(requestedProvince, candidate.Province)
                || MatchesAnyMetadataToken(candidate, requestedProvince, "provinceCodes", "province_codes", "province", "area");
        }

        // For broadcast, a provincial brief should match the station's primary province,
        // not any spillover province listed in broader coverage metadata.
        return Matches(requestedProvince, candidate.Province)
            || Matches(requestedProvince, candidate.RegionClusterCode)
            || MatchesAnyMetadataToken(candidate, requestedProvince, "primaryProvinceCode", "primary_province_code", "province", "province_code");
    }

    private bool MatchesPriorityArea(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var priorityAreas = request.Targeting?.PriorityAreas ?? request.MustHaveAreas;
        return priorityAreas.Any(area =>
            Matches(area, candidate.Suburb)
            || Matches(area, candidate.Area)
            || Matches(area, candidate.City)
            || Matches(area, candidate.Province));
    }

    private bool MatchesBusinessOrigin(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var businessLocation = request.BusinessLocation;
        if (businessLocation is null)
        {
            return false;
        }

        return Matches(businessLocation.Area, candidate.Suburb)
            || Matches(businessLocation.Area, candidate.Area)
            || Matches(businessLocation.City, candidate.City)
            || Matches(businessLocation.Province, candidate.Province);
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
            : PlanningChannelSupport.NormalizeChannel(mediaType);
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

}

using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

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

        if (request.Cities.Any(x => Matches(x, candidate.City)))
        {
            return true;
        }

        if (request.Provinces.Any(x => Matches(x, candidate.Province)))
        {
            return true;
        }

        if (request.Areas.Any(x => Matches(x, candidate.Area) || Matches(x, candidate.Suburb)))
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
}


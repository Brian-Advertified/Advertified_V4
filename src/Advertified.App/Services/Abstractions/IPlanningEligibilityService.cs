using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningEligibilityService
{
    PlanningPolicyOutcome FilterEligibleCandidates(List<InventoryCandidate> candidates, CampaignPlanningRequest request);
}


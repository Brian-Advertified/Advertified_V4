using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningBudgetAllocationService
{
    PlanningBudgetAllocation Resolve(CampaignPlanningRequest request);
    PlanningBudgetAllocation RebalanceChannelTargets(CampaignPlanningRequest request, IReadOnlyDictionary<string, int> channelShares);
}

using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IBroadcastPlanningInventorySource
{
    Task<List<BroadcastPlanningInventorySeed>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<BroadcastPlanningInventorySeed>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<BroadcastPlanningInventorySeed>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
}


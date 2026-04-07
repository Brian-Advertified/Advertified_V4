using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface ISocialPlanningInventorySource
{
    Task<List<BroadcastPlanningInventorySeed>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
}

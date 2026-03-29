using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IOohPlanningInventorySource
{
    Task<List<OohPlanningInventoryRow>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
}


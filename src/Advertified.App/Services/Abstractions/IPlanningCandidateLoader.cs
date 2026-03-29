using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningCandidateLoader
{
    Task<List<InventoryCandidate>> LoadCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
}


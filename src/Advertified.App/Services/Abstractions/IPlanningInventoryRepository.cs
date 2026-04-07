using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningInventoryRepository
{
    Task<BroadcastInventoryCandidateSet> GetBroadcastCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<InventoryCandidate>> GetDigitalCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<InventoryCandidate>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<InventoryCandidate>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
    Task<List<InventoryCandidate>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken);
}

public sealed record BroadcastInventoryCandidateSet(
    List<InventoryCandidate> RadioSlots,
    List<InventoryCandidate> RadioPackages,
    List<InventoryCandidate> Tv);

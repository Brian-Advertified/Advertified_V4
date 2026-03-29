using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PlanningInventoryRepository : IPlanningInventoryRepository
{
    private readonly IOohPlanningInventorySource _oohSource;
    private readonly IBroadcastPlanningInventorySource _broadcastSource;
    private readonly IPlanningInventoryCandidateMapper _candidateMapper;

    public PlanningInventoryRepository(
        IOohPlanningInventorySource oohSource,
        IBroadcastPlanningInventorySource broadcastSource,
        IPlanningInventoryCandidateMapper candidateMapper)
    {
        _oohSource = oohSource;
        _broadcastSource = broadcastSource;
        _candidateMapper = candidateMapper;
    }

    public async Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var rows = await _oohSource.GetCandidatesAsync(request, cancellationToken);
        return rows.Select(_candidateMapper.MapOoh).ToList();
    }

    public async Task<List<InventoryCandidate>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var seeds = await _broadcastSource.GetRadioSlotCandidatesAsync(request, cancellationToken);
        return seeds.Select(_candidateMapper.MapBroadcast).ToList();
    }

    public async Task<List<InventoryCandidate>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var seeds = await _broadcastSource.GetRadioPackageCandidatesAsync(request, cancellationToken);
        return seeds.Select(_candidateMapper.MapBroadcast).ToList();
    }

    public async Task<List<InventoryCandidate>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var seeds = await _broadcastSource.GetTvCandidatesAsync(request, cancellationToken);
        return seeds.Select(_candidateMapper.MapBroadcast).ToList();
    }
}

using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PlanningInventoryRepository : IPlanningInventoryRepository
{
    private readonly IOohPlanningInventorySource _oohSource;
    private readonly IBroadcastPlanningInventorySource _broadcastSource;
    private readonly ISocialPlanningInventorySource _socialSource;
    private readonly IPlanningInventoryCandidateMapper _candidateMapper;

    public PlanningInventoryRepository(
        IOohPlanningInventorySource oohSource,
        IBroadcastPlanningInventorySource broadcastSource,
        ISocialPlanningInventorySource socialSource,
        IPlanningInventoryCandidateMapper candidateMapper)
    {
        _oohSource = oohSource;
        _broadcastSource = broadcastSource;
        _socialSource = socialSource;
        _candidateMapper = candidateMapper;
    }

    public async Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var rows = await _oohSource.GetCandidatesAsync(request, cancellationToken);
        return rows.Select(_candidateMapper.MapOoh).ToList();
    }

    public async Task<BroadcastInventoryCandidateSet> GetBroadcastCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var seeds = await _broadcastSource.GetCandidatesAsync(request, cancellationToken);
        return new BroadcastInventoryCandidateSet(
            RadioSlots: seeds.RadioSlots.Select(_candidateMapper.MapBroadcast).ToList(),
            RadioPackages: seeds.RadioPackages.Select(_candidateMapper.MapBroadcast).ToList(),
            Tv: seeds.Tv.Select(_candidateMapper.MapBroadcast).ToList(),
            Newspapers: seeds.Newspapers.Select(_candidateMapper.MapBroadcast).ToList());
    }

    public async Task<List<InventoryCandidate>> GetDigitalCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var seeds = await _socialSource.GetCandidatesAsync(request, cancellationToken);
        return seeds.Select(_candidateMapper.MapBroadcast).ToList();
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

    public async Task<List<InventoryCandidate>> GetNewspaperCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var seeds = await _broadcastSource.GetNewspaperCandidatesAsync(request, cancellationToken);
        return seeds.Select(_candidateMapper.MapBroadcast).ToList();
    }
}

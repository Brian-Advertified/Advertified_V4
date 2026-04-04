using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PlanningCandidateLoader : IPlanningCandidateLoader
{
    private readonly IPlanningInventoryRepository _repository;

    public PlanningCandidateLoader(IPlanningInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<InventoryCandidate>> LoadCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var oohTask = _repository.GetOohCandidatesAsync(request, cancellationToken);
        var broadcastTask = _repository.GetBroadcastCandidatesAsync(request, cancellationToken);

        await Task.WhenAll(oohTask, broadcastTask);

        var ooh = await oohTask;
        var broadcast = await broadcastTask;

        return ooh
            .Concat(broadcast.RadioSlots)
            .Concat(broadcast.RadioPackages)
            .Concat(broadcast.Tv)
            .ToList();
    }
}

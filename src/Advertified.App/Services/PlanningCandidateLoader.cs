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
        // These repository calls can share the same EF-backed dependencies, so
        // they must not run concurrently on the same request scope.
        var ooh = await _repository.GetOohCandidatesAsync(request, cancellationToken);
        var broadcast = await _repository.GetBroadcastCandidatesAsync(request, cancellationToken);
        var digital = await _repository.GetDigitalCandidatesAsync(request, cancellationToken);

        return ooh
            .Concat(digital)
            .Concat(broadcast.RadioSlots)
            .Concat(broadcast.RadioPackages)
            .Concat(broadcast.Tv)
            .ToList();
    }
}

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
        var ooh = await _repository.GetOohCandidatesAsync(request, cancellationToken);
        var radioSlots = await _repository.GetRadioSlotCandidatesAsync(request, cancellationToken);
        var radioPackages = await _repository.GetRadioPackageCandidatesAsync(request, cancellationToken);
        var tv = await _repository.GetTvCandidatesAsync(request, cancellationToken);

        return ooh
            .Concat(radioSlots)
            .Concat(radioPackages)
            .Concat(tv)
            .ToList();
    }
}


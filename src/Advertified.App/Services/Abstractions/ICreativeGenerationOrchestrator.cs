using Advertified.App.Contracts.Creatives;

namespace Advertified.App.Services.Abstractions;

public interface ICreativeGenerationOrchestrator
{
    Task<GenerateCreativesResponse> GenerateAsync(
        GenerateCreativesRequest request,
        Guid? sourceCreativeSystemId,
        bool persistOutputs,
        CancellationToken cancellationToken);

    Task<GenerateCreativesResponse> RegenerateAsync(
        RegenerateCreativeRequest request,
        CancellationToken cancellationToken);

    Task<GenerateCreativesRequest> BuildNormalizedRequestFromCampaignAsync(
        Guid campaignId,
        string prompt,
        string? objective,
        string? tone,
        IReadOnlyList<string>? channels,
        CancellationToken cancellationToken);

    LocalisationResponse Localize(LocalisationRequest request);
}

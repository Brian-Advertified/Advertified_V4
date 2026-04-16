namespace Advertified.App.Services.Abstractions;

public interface IBroadcastInventoryIntelligenceService
{
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetRadioIntelligenceByInternalKeyAsync(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetTvIntelligenceByInternalKeyAsync(CancellationToken cancellationToken);
}


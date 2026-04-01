using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Infrastructure;

public sealed class MultiAiOrchestrator : IMultiAiOrchestrator
{
    private readonly IAiProviderStrategyFactory _factory;

    public MultiAiOrchestrator(IAiProviderStrategyFactory factory)
    {
        _factory = factory;
    }

    public async Task<string> ExecuteAsync(AdvertisingChannel channel, string operation, string inputJson, CancellationToken cancellationToken)
    {
        var strategy = _factory.GetRequired(channel, operation);
        return await strategy.ExecuteAsync(inputJson, cancellationToken);
    }
}

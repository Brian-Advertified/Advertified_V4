using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Infrastructure;

public sealed class AiProviderStrategyFactory : IAiProviderStrategyFactory
{
    private readonly IReadOnlyList<IAiProviderStrategy> _strategies;

    public AiProviderStrategyFactory(IEnumerable<IAiProviderStrategy> strategies)
    {
        _strategies = strategies.ToArray();
    }

    public IAiProviderStrategy GetRequired(AdvertisingChannel channel, string operation)
    {
        return _strategies.FirstOrDefault(strategy => strategy.CanHandle(channel, operation))
            ?? throw new InvalidOperationException($"No provider strategy registered for channel '{channel}' and operation '{operation}'.");
    }
}

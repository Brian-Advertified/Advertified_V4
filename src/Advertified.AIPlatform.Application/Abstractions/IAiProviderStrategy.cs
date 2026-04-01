using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Application.Abstractions;

public interface IAiProviderStrategy
{
    string ProviderName { get; }

    bool CanHandle(AdvertisingChannel channel, string operation);

    Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken);
}

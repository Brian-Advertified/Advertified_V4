using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Application.Abstractions;

public interface IMultiAiOrchestrator
{
    Task<string> ExecuteAsync(AdvertisingChannel channel, string operation, string inputJson, CancellationToken cancellationToken);
}

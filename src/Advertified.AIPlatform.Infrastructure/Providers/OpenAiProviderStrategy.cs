using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Infrastructure.Providers;

public sealed class OpenAiProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "OpenAI";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return operation is "creative-generate" or "creative-qa" or "orchestration";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        // Deterministic placeholder response while provider integration is wired.
        var payload = $"{{\"provider\":\"OpenAI\",\"result\":\"ok\",\"input\":{inputJson}}}";
        return Task.FromResult(payload);
    }
}

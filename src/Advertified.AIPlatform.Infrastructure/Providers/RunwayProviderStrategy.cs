using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Infrastructure.Providers;

public sealed class RunwayProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "Runway";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return (channel == AdvertisingChannel.Tv || channel == AdvertisingChannel.Digital) && operation == "asset-video";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        return Task.FromResult("{\"provider\":\"Runway\",\"assetType\":\"video\",\"assetUrl\":\"https://assets.example.com/video.mp4\"}");
    }
}

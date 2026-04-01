using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Infrastructure.Providers;

public sealed class ImageProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "ImageProvider";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return (channel == AdvertisingChannel.Billboard || channel == AdvertisingChannel.Digital) && operation == "asset-image";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        return Task.FromResult("{\"provider\":\"ImageProvider\",\"assetType\":\"image\",\"assetUrl\":\"https://assets.example.com/image.png\"}");
    }
}

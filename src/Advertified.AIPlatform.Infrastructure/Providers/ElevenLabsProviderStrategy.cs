using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Infrastructure.Providers;

public sealed class ElevenLabsProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "ElevenLabs";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return channel == AdvertisingChannel.Radio && operation == "asset-voice";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        return Task.FromResult("{\"provider\":\"ElevenLabs\",\"assetType\":\"voice\",\"assetUrl\":\"https://assets.example.com/voice.mp3\"}");
    }
}

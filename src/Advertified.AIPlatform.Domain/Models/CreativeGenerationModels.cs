namespace Advertified.AIPlatform.Domain.Models;

public sealed record ChannelCreativeOutput(
    AdvertisingChannel Channel,
    string Language,
    string PayloadJson,
    double? Score = null);

public sealed record CreativeGenerationResult(
    Guid CampaignId,
    Guid JobId,
    DateTimeOffset CompletedAt,
    IReadOnlyList<ChannelCreativeOutput> Creatives);

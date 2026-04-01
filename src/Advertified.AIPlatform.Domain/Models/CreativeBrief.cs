namespace Advertified.AIPlatform.Domain.Models;

public sealed record CreativeBrief(
    Guid CampaignId,
    string Brand,
    string Objective,
    string Tone,
    IReadOnlyList<string> Languages,
    IReadOnlyList<AdvertisingChannel> Channels,
    string KeyMessage,
    string CallToAction,
    IReadOnlyList<string> AudienceInsights);

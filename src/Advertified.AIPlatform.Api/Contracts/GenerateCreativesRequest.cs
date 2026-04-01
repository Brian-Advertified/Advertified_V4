namespace Advertified.AIPlatform.Api.Contracts;

public sealed record GenerateCreativesRequest(
    Guid CampaignId,
    string Brand,
    string Objective,
    string Tone,
    string KeyMessage,
    string CallToAction,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Channels,
    IReadOnlyList<string>? AudienceInsights);

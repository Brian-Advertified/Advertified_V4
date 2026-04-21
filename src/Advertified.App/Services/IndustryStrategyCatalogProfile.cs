namespace Advertified.App.Services;

public sealed class IndustryStrategyCatalogProfile
{
    public string IndustryCode { get; init; } = string.Empty;

    public string IndustryLabel { get; init; } = string.Empty;

    public IndustryAudienceProfile Audience { get; init; } = new();

    public IndustryCampaignProfile Campaign { get; init; } = new();

    public IndustryChannelProfile Channels { get; init; } = new();

    public IndustryCreativeProfile Creative { get; init; } = new();

    public IndustryComplianceProfile Compliance { get; init; } = new();

    public IndustryResearchProfile Research { get; init; } = new();
}

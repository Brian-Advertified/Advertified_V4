namespace Advertified.App.Contracts.Leads;

public sealed class LeadOpportunityProfileDto
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string SuggestedCampaignType { get; init; } = string.Empty;

    public IReadOnlyList<string> DetectedGaps { get; init; } = Array.Empty<string>();

    public string ExpectedOutcome { get; init; } = string.Empty;

    public IReadOnlyList<string> RecommendedChannels { get; init; } = Array.Empty<string>();

    public string WhyActNow { get; init; } = string.Empty;
}

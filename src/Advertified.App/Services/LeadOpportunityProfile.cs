namespace Advertified.App.Services;

public sealed class LeadOpportunityProfile
{
    public string Key { get; init; } = "passive_untapped_business";

    public string Name { get; init; } = "Passive / Untapped Business";

    public string SuggestedCampaignType { get; init; } = "awareness";

    public IReadOnlyList<string> DetectedGaps { get; init; } = Array.Empty<string>();

    public string ExpectedOutcome { get; init; } = string.Empty;

    public IReadOnlyList<string> RecommendedChannels { get; init; } = Array.Empty<string>();

    public string WhyActNow { get; init; } = string.Empty;
}

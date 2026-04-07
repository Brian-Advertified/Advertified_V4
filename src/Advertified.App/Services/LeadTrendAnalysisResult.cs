namespace Advertified.App.Services;

public sealed class LeadTrendAnalysisResult
{
    public string Summary { get; init; } = string.Empty;

    public bool CampaignStartedRecently { get; init; }

    public bool ActivityIncreased { get; init; }
}

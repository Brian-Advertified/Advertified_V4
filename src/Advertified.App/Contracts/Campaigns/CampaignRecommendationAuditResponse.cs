namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignRecommendationAuditResponse
{
    public string RequestSummary { get; set; } = string.Empty;
    public string SelectionSummary { get; set; } = string.Empty;
    public string RejectionSummary { get; set; } = string.Empty;
    public string PolicySummary { get; set; } = string.Empty;
    public string BudgetSummary { get; set; } = string.Empty;
    public string? FallbackSummary { get; set; }
}

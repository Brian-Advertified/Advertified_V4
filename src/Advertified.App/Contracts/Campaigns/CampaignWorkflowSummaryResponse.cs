namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignWorkflowSummaryResponse
{
    public string CurrentStateKey { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;
    public bool RequiresClientAction { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string PaymentState { get; set; } = string.Empty;
    public bool PaymentAwaitingManualReview { get; set; }
    public bool PaymentRequiredBeforeApproval { get; set; }
    public bool HasClearedPayment { get; set; }
    public bool RecommendationAwaitingDecision { get; set; }
    public bool RecommendationApprovalCompleted { get; set; }
    public bool CanOpenBrief { get; set; }
    public bool CanOpenPlanning { get; set; }
}

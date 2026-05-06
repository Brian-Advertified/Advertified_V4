namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignRecommendationResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string ProposalLabel { get; set; } = string.Empty;
    public string ProposalStrategy { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public CampaignRecommendationNarrativeResponse? Narrative { get; set; }
    public string? ClientFeedbackNotes { get; set; }
    public bool ManualReviewRequired { get; set; }
    public IReadOnlyList<string> FallbackFlags { get; set; } = Array.Empty<string>();
    public CampaignRecommendationAuditResponse? Audit { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public decimal EstimatedSupplierCost { get; set; }
    public decimal EstimatedGrossProfit { get; set; }
    public decimal? EstimatedGrossMarginPercent { get; set; }
    public string MarginStatus { get; set; } = string.Empty;
    public string? ClientExplanation { get; set; }
    public string SupplierAvailabilityStatus { get; set; } = string.Empty;
    public DateTimeOffset? SupplierAvailabilityCheckedAt { get; set; }
    public string? SupplierAvailabilityNotes { get; set; }
    public IReadOnlyList<EmailDeliveryAttemptResponse> EmailDeliveries { get; set; } = Array.Empty<EmailDeliveryAttemptResponse>();
    public IReadOnlyList<RecommendationItemResponse> Items { get; set; } = Array.Empty<RecommendationItemResponse>();
}

public sealed class CampaignRecommendationNarrativeResponse
{
    public string? ClientChallenge { get; set; }
    public string? StrategicApproach { get; set; }
    public string? ExpectedOutcome { get; set; }
    public IReadOnlyList<string> ChannelRoles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SuccessMeasures { get; set; } = Array.Empty<string>();
}

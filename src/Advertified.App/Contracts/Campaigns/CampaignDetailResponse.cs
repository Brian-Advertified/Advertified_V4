namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignDetailResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Industry { get; set; }
    public Guid PackageOrderId { get; set; }
    public Guid PackageBandId { get; set; }
    public string PackageBandName { get; set; } = string.Empty;
    public decimal SelectedBudget { get; set; }
    public string OrderIntent { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? CampaignName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PlanningMode { get; set; }
    public bool AiUnlocked { get; set; }
    public bool AgentAssistanceRequested { get; set; }
    public Guid? AssignedAgentUserId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public bool IsAssignedToCurrentUser { get; set; }
    public bool IsUnassigned { get; set; }
    public string NextAction { get; set; } = string.Empty;
    public CampaignLifecycleResponse Lifecycle { get; set; } = new();
    public CampaignSendValidationResponse SendValidation { get; set; } = new();
    public ProspectDispositionResponse ProspectDisposition { get; set; } = new();
    public CampaignBusinessProcessResponse BusinessProcess { get; set; } = new();
    public CampaignPlanningTargetResponse? BusinessLocation { get; set; }
    public CampaignPlanningTargetResponse? EffectivePlanningTarget { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<CampaignTimelineStepResponse> Timeline { get; set; } = Array.Empty<CampaignTimelineStepResponse>();
    public SaveCampaignBriefRequest? Brief { get; set; }
    public IReadOnlyList<CampaignRecommendationResponse> Recommendations { get; set; } = Array.Empty<CampaignRecommendationResponse>();
    public CampaignRecommendationResponse? Recommendation { get; set; }
    public string? RecommendationPdfUrl { get; set; }
    public IReadOnlyList<CampaignCreativeSystemResponse> CreativeSystems { get; set; } = Array.Empty<CampaignCreativeSystemResponse>();
    public CampaignCreativeSystemResponse? LatestCreativeSystem { get; set; }
    public IReadOnlyList<CampaignAssetResponse> Assets { get; set; } = Array.Empty<CampaignAssetResponse>();
    public IReadOnlyList<CampaignSupplierBookingResponse> SupplierBookings { get; set; } = Array.Empty<CampaignSupplierBookingResponse>();
    public IReadOnlyList<CampaignDeliveryReportResponse> DeliveryReports { get; set; } = Array.Empty<CampaignDeliveryReportResponse>();
    public IReadOnlyList<CampaignExecutionTaskResponse> ExecutionTasks { get; set; } = Array.Empty<CampaignExecutionTaskResponse>();
    public IReadOnlyList<CampaignPerformanceTimelinePointResponse> PerformanceTimeline { get; set; } = Array.Empty<CampaignPerformanceTimelinePointResponse>();
    public DateOnly? EffectiveEndDate { get; set; }
    public int? DaysLeft { get; set; }
}

public sealed class CampaignBusinessProcessResponse
{
    public RevenueAttributionResponse RevenueAttribution { get; set; } = new();
    public LostReasonResponse LostReason { get; set; } = new();
    public RecommendationCommercialCheckResponse RecommendationCommercialCheck { get; set; } = new();
    public SupplierReadinessResponse SupplierReadiness { get; set; } = new();
    public PostCampaignGrowthResponse PostCampaignGrowth { get; set; } = new();
    public TermsAcceptanceResponse TermsAcceptance { get; set; } = new();
    public ClientRefundCancellationResponse RefundCancellation { get; set; } = new();
}

public sealed class RevenueAttributionResponse
{
    public Guid? AgentUserId { get; set; }
    public string? AgentName { get; set; }
    public string Geography { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, decimal> ChannelSpend { get; set; } = new Dictionary<string, decimal>();
    public decimal PaidRevenue { get; set; }
}

public sealed class LostReasonResponse
{
    public string? Stage { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? LostAt { get; set; }
}

public sealed class RecommendationCommercialCheckResponse
{
    public Guid? RecommendationId { get; set; }
    public decimal TotalCost { get; set; }
    public decimal EstimatedSupplierCost { get; set; }
    public decimal EstimatedGrossProfit { get; set; }
    public decimal? EstimatedGrossMarginPercent { get; set; }
    public string MarginStatus { get; set; } = string.Empty;
}

public sealed class SupplierReadinessResponse
{
    public string Status { get; set; } = string.Empty;
    public int ConfirmedBookings { get; set; }
    public int UnconfirmedBookings { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class PostCampaignGrowthResponse
{
    public string ReportingStatus { get; set; } = string.Empty;
    public bool RenewalRecommended { get; set; }
    public string NextAction { get; set; } = string.Empty;
}

public sealed class TermsAcceptanceResponse
{
    public bool Accepted { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? Version { get; set; }
    public string? Source { get; set; }
}

public sealed class ClientRefundCancellationResponse
{
    public string RefundStatus { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public string? RefundReason { get; set; }
    public DateTimeOffset? RefundProcessedAt { get; set; }
    public string CancellationStatus { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
}

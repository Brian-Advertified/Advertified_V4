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
    public DateOnly? EffectiveEndDate { get; set; }
    public int? DaysLeft { get; set; }
}

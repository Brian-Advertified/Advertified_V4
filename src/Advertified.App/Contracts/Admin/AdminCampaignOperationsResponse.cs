namespace Advertified.App.Contracts.Admin;

public sealed class AdminCampaignOperationsResponse
{
    public IReadOnlyList<AdminCampaignOperationsItemResponse> Items { get; set; } = Array.Empty<AdminCampaignOperationsItemResponse>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    public string SortBy { get; set; } = "delivery_risk";
    public bool AttentionOnly { get; set; }
    public int PerformanceAttentionThresholdPercent { get; set; }
    public int TotalPausedCount { get; set; }
    public int TotalRefundAttentionCount { get; set; }
    public int TotalScheduledCount { get; set; }
    public int TotalPerformanceAttentionCount { get; set; }
}

public sealed class AdminCampaignOperationsItemResponse
{
    public Guid CampaignId { get; set; }
    public Guid PackageOrderId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string CampaignStatus { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageBandName { get; set; } = string.Empty;
    public decimal SelectedBudget { get; set; }
    public decimal ChargedTotal { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string RefundStatus { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public decimal RemainingCollectedAmount { get; set; }
    public decimal SuggestedRefundAmount { get; set; }
    public decimal MaxManualRefundAmount { get; set; }
    public decimal GatewayFeeRetainedAmount { get; set; }
    public string RefundPolicyStage { get; set; } = string.Empty;
    public string RefundPolicyLabel { get; set; } = string.Empty;
    public string RefundPolicySummary { get; set; } = string.Empty;
    public string? RefundReason { get; set; }
    public DateTimeOffset? RefundProcessedAt { get; set; }
    public bool IsPaused { get; set; }
    public string? PauseReason { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public int TotalPausedDays { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public int? DaysLeft { get; set; }
    public bool CanPause { get; set; }
    public bool CanUnpause { get; set; }
    public bool CanProcessRefund { get; set; }
    public decimal PerformanceBookedSpend { get; set; }
    public decimal PerformanceDeliveredSpend { get; set; }
    public int PerformanceDeliveryPercent { get; set; }
    public long PerformanceImpressions { get; set; }
    public int PerformancePlaysOrSpots { get; set; }
    public int PerformanceSyncedClicks { get; set; }
    public DateOnly? PerformanceLatestReportDate { get; set; }
}

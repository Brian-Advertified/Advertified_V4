namespace Advertified.App.Contracts.Admin;

public sealed class AdminAiMonthlyCostReportRow
{
    public string Month { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal TotalEstimatedCostZar { get; set; }
    public decimal TotalActualCostZar { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int RejectedJobs { get; set; }
}

public sealed class AdminAiReplayResponse
{
    public Guid ReplayedFromJobId { get; set; }
    public Guid NewJobId { get; set; }
    public Guid CampaignId { get; set; }
    public string Pipeline { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
}

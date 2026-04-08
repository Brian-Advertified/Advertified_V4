namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPerformanceSnapshotResponse
{
    public Guid CampaignId { get; set; }
    public decimal TotalBookedSpend { get; set; }
    public decimal TotalDeliveredSpend { get; set; }
    public long TotalImpressions { get; set; }
    public int TotalPlaysOrSpots { get; set; }
    public int TotalSyncedClicks { get; set; }
    public int BookingCount { get; set; }
    public int ReportCount { get; set; }
    public int SpendDeliveryPercent { get; set; }
    public DateOnly? LatestReportDate { get; set; }
    public IReadOnlyList<CampaignPerformanceTimelinePointResponse> Timeline { get; set; } = Array.Empty<CampaignPerformanceTimelinePointResponse>();
    public IReadOnlyList<CampaignPerformanceChannelResponse> Channels { get; set; } = Array.Empty<CampaignPerformanceChannelResponse>();
}

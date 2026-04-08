namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPerformanceChannelResponse
{
    public string Channel { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal BookedSpend { get; set; }
    public decimal DeliveredSpend { get; set; }
    public long Impressions { get; set; }
    public int PlaysOrSpots { get; set; }
    public int SyncedClicks { get; set; }
    public int BookingCount { get; set; }
    public int ReportCount { get; set; }
}

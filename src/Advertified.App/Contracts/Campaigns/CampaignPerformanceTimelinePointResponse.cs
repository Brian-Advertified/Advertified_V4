namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPerformanceTimelinePointResponse
{
    public DateOnly Date { get; set; }
    public long Impressions { get; set; }
    public int PlaysOrSpots { get; set; }
    public decimal SpendDelivered { get; set; }
}

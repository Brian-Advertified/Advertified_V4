namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPerformanceTimelinePointResponse
{
    public DateOnly Date { get; set; }
    public long Impressions { get; set; }
    public int PlaysOrSpots { get; set; }
    public int Leads { get; set; }
    public decimal? CplZar { get; set; }
    public decimal? Roas { get; set; }
    public decimal SpendDelivered { get; set; }
}

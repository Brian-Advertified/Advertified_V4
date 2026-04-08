namespace Advertified.App.Data.Entities;

public sealed class CampaignChannelMetric
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateOnly MetricDate { get; set; }
    public decimal SpendZar { get; set; }
    public long Impressions { get; set; }
    public int Clicks { get; set; }
    public int Leads { get; set; }
    public decimal AttributedRevenueZar { get; set; }
    public decimal? CplZar { get; set; }
    public decimal? Roas { get; set; }
    public string SourceType { get; set; } = "ad_platform_sync";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
}

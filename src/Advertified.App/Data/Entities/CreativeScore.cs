namespace Advertified.App.Data.Entities;

public sealed class CreativeScore
{
    public Guid Id { get; set; }
    public Guid CampaignCreativeId { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public decimal MetricValue { get; set; }
    public DateTime CreatedAt { get; set; }

    public CampaignCreative CampaignCreative { get; set; } = null!;
}

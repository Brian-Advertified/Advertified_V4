namespace Advertified.App.Data.Entities;

public sealed class CampaignCreative
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? SourceCreativeSystemId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string CreativeType { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = "{}";
    public decimal? Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
    public CampaignCreativeSystem? SourceCreativeSystem { get; set; }
    public ICollection<CreativeScore> CreativeScores { get; set; } = new List<CreativeScore>();
}

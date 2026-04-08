namespace Advertified.App.Data.Entities;

public sealed class CampaignAdPlatformLink
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid AdPlatformConnectionId { get; set; }
    public string? ExternalCampaignId { get; set; }
    public bool IsPrimary { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
    public AdPlatformConnection AdPlatformConnection { get; set; } = null!;
}


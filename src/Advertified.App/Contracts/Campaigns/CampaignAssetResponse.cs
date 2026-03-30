namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignAssetResponse
{
    public Guid Id { get; set; }

    public string AssetType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? PublicUrl { get; set; }

    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

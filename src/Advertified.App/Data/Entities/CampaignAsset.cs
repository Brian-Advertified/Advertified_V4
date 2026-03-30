using System;

namespace Advertified.App.Data.Entities;

public partial class CampaignAsset
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public string AssetType { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string StorageObjectKey { get; set; } = null!;

    public string? PublicUrl { get; set; }

    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount? UploadedByUser { get; set; }
}

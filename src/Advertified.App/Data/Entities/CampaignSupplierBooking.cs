using System;

namespace Advertified.App.Data.Entities;

public partial class CampaignSupplierBooking
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ProofAssetId { get; set; }

    public string SupplierOrStation { get; set; } = null!;

    public string Channel { get; set; } = null!;

    public string BookingStatus { get; set; } = null!;

    public decimal CommittedAmount { get; set; }

    public DateTime? BookedAt { get; set; }

    public DateOnly? LiveFrom { get; set; }

    public DateOnly? LiveTo { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual CampaignAsset? ProofAsset { get; set; }
}

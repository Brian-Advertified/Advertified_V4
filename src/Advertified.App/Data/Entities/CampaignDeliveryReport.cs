using System;

namespace Advertified.App.Data.Entities;

public partial class CampaignDeliveryReport
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid? SupplierBookingId { get; set; }

    public Guid? EvidenceAssetId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string ReportType { get; set; } = null!;

    public string Headline { get; set; } = null!;

    public string? Summary { get; set; }

    public DateTime? ReportedAt { get; set; }

    public long? Impressions { get; set; }

    public int? PlaysOrSpots { get; set; }

    public decimal? SpendDelivered { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual CampaignAsset? EvidenceAsset { get; set; }

    public virtual CampaignSupplierBooking? SupplierBooking { get; set; }
}

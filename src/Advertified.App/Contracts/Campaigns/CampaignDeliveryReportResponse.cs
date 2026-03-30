namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignDeliveryReportResponse
{
    public Guid Id { get; set; }

    public Guid? SupplierBookingId { get; set; }

    public string ReportType { get; set; } = string.Empty;

    public string Headline { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public DateTimeOffset? ReportedAt { get; set; }

    public long? Impressions { get; set; }

    public int? PlaysOrSpots { get; set; }

    public decimal? SpendDelivered { get; set; }

    public CampaignAssetResponse? EvidenceAsset { get; set; }
}

namespace Advertified.App.Contracts.Agent;

public sealed class SaveCampaignDeliveryReportRequest
{
    public Guid? SupplierBookingId { get; set; }

    public string ReportType { get; set; } = string.Empty;

    public string Headline { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public DateTimeOffset? ReportedAt { get; set; }

    public long? Impressions { get; set; }

    public int? PlaysOrSpots { get; set; }

    public decimal? SpendDelivered { get; set; }

    public Guid? EvidenceAssetId { get; set; }
}

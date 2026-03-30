namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignSupplierBookingResponse
{
    public Guid Id { get; set; }

    public string SupplierOrStation { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string BookingStatus { get; set; } = string.Empty;

    public decimal CommittedAmount { get; set; }

    public DateTimeOffset? BookedAt { get; set; }

    public DateOnly? LiveFrom { get; set; }

    public DateOnly? LiveTo { get; set; }

    public string? Notes { get; set; }

    public CampaignAssetResponse? ProofAsset { get; set; }
}

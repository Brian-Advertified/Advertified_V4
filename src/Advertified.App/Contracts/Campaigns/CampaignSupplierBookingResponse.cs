namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignSupplierBookingResponse
{
    public Guid Id { get; set; }

    public string SupplierOrStation { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string BookingStatus { get; set; } = string.Empty;

    public string AvailabilityStatus { get; set; } = string.Empty;

    public DateTimeOffset? AvailabilityCheckedAt { get; set; }

    public string? SupplierConfirmationReference { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public decimal CommittedAmount { get; set; }

    public DateTimeOffset? BookedAt { get; set; }

    public DateOnly? LiveFrom { get; set; }

    public DateOnly? LiveTo { get; set; }

    public string? Notes { get; set; }

    public CampaignAssetResponse? ProofAsset { get; set; }
}

namespace Advertified.App.Contracts.Agent;

public sealed class SaveCampaignSupplierBookingRequest
{
    public string SupplierOrStation { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string BookingStatus { get; set; } = "planned";

    public string? AvailabilityStatus { get; set; }

    public DateTimeOffset? AvailabilityCheckedAt { get; set; }

    public string? SupplierConfirmationReference { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public decimal CommittedAmount { get; set; }

    public DateTimeOffset? BookedAt { get; set; }

    public DateOnly? LiveFrom { get; set; }

    public DateOnly? LiveTo { get; set; }

    public string? Notes { get; set; }

    public Guid? ProofAssetId { get; set; }
}

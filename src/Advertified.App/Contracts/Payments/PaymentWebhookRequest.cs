namespace Advertified.App.Contracts.Payments;

public sealed class PaymentWebhookRequest
{
    public Guid PackageOrderId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string? PaymentStatus { get; set; }
}

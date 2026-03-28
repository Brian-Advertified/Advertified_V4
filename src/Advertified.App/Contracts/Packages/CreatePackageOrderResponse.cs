namespace Advertified.App.Contracts.Packages;

public sealed class CreatePackageOrderResponse
{
    public Guid PackageOrderId { get; set; }
    public Guid PackageBandId { get; set; }
    public string PaymentStatus { get; set; } = "pending";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string PaymentProvider { get; set; } = "vodapay";
    public string? CheckoutUrl { get; set; }
    public string? CheckoutSessionId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceStatus { get; set; }
    public string? InvoicePdfUrl { get; set; }
}

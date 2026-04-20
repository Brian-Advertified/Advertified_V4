namespace Advertified.App.Contracts.Packages;

public sealed class PackageOrderDetailResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid PackageBandId { get; set; }
    public string PackageBandName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string OrderIntent { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string RefundStatus { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public decimal GatewayFeeRetainedAmount { get; set; }
    public string? RefundReason { get; set; }
    public DateTimeOffset? RefundProcessedAt { get; set; }
    public string? PaymentReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CheckoutSessionId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceStatus { get; set; }
    public string? InvoicePdfUrl { get; set; }
}

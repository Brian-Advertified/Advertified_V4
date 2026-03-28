namespace Advertified.App.Contracts.Packages;

public sealed class PackageOrderListItemResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PackageBandId { get; set; }
    public string PackageBandName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string PaymentProvider { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceStatus { get; set; }
    public string? InvoicePdfUrl { get; set; }
}

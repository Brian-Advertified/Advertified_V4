namespace Advertified.App.Contracts.Payments;

public sealed class PaymentStateCacheEntry
{
    public string Status { get; set; } = "pending";
    public decimal Amount { get; set; }
    public string Package { get; set; } = string.Empty;
    public string Currency { get; set; } = "ZAR";
    public string Provider { get; set; } = string.Empty;
    public Guid PackageOrderId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

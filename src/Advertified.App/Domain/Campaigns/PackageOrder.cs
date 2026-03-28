namespace Advertified.App.Domain.Campaigns;

public sealed class PackageOrder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PackageBandId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string PaymentProvider { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public string PaymentStatus { get; set; } = "pending";
    public DateTimeOffset? PurchasedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

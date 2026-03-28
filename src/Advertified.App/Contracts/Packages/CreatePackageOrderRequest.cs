namespace Advertified.App.Contracts.Packages;

public sealed class CreatePackageOrderRequest
{
    public Guid PackageBandId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string PaymentProvider { get; set; } = "vodapay";
}

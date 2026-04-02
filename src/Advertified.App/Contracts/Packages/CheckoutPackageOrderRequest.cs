namespace Advertified.App.Contracts.Packages;

public sealed class CheckoutPackageOrderRequest
{
    public string PaymentProvider { get; set; } = "vodapay";
}

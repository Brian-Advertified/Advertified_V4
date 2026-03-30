namespace Advertified.App.Contracts.Admin;

public sealed class AdminUpdatePackageOrderPaymentStatusRequest
{
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}

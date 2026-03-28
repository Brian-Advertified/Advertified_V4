namespace Advertified.App.Contracts.Payments;

public sealed class VodaPayCallbackCaptureRequest
{
    public Guid PackageOrderId { get; set; }
    public Dictionary<string, string> QueryParameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

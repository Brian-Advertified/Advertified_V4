namespace Advertified.App.Contracts.Payments;

public sealed class VodaPayCheckoutSession
{
    public string SessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string EchoData { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
}

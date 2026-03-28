namespace Advertified.App.Contracts.Payments;

public sealed class VodaPayInitiateResponse
{
    public bool Succeeded { get; set; }
    public VodaPayInitiateResponseData? Data { get; set; }
}

public sealed class VodaPayInitiateResponseData
{
    public string? EchoData { get; set; }
    public string? ResponseCode { get; set; }
    public string? ResponseMessage { get; set; }
    public DateTimeOffset? TransmissionDateTime { get; set; }
    public string? TraceId { get; set; }
    public string? SessionId { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentReference { get; set; }
    public string? InitiationUrl { get; set; }
    public string? PaymentUrl { get; set; }
    public string? CheckoutUrl { get; set; }
}

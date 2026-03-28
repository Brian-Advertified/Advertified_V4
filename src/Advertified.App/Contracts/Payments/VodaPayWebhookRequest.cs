namespace Advertified.App.Contracts.Payments;

public sealed class VodaPayWebhookRequest
{
    public string? EchoData { get; set; }
    public DateTimeOffset? TransmissionDateTime { get; set; }
    public string? PaymentToken { get; set; }
    public string? PaymentReference { get; set; }
    public string? SessionId { get; set; }
    public string? ResponseCode { get; set; }
    public string? ResponseMessage { get; set; }
    public string? RetrievalReferenceNumber { get; set; }
    public string? RetrievalReferenceNumberExtended { get; set; }
    public string? MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public decimal? TransactionAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? TransactionId { get; set; }
    public Dictionary<string, string?>? TransactionInfo { get; set; }
}

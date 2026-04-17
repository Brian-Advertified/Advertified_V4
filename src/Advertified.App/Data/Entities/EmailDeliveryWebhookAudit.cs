namespace Advertified.App.Data.Entities;

public sealed class EmailDeliveryWebhookAudit
{
    public Guid Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string? WebhookMessageId { get; set; }
    public string? EventType { get; set; }
    public bool SignatureValid { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ProcessingNotes { get; set; }
    public string? HeadersJson { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

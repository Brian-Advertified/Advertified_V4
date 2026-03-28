namespace Advertified.App.Data.Entities;

public sealed class PaymentProviderWebhookAudit
{
    public Guid Id { get; set; }

    public Guid? PackageOrderId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string WebhookPath { get; set; } = string.Empty;

    public string HeadersJson { get; set; } = "{}";

    public string BodyJson { get; set; } = string.Empty;

    public string ProcessedStatus { get; set; } = string.Empty;

    public string? ProcessedMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public PackageOrder? PackageOrder { get; set; }
}

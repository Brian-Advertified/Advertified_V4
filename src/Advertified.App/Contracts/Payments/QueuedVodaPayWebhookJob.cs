namespace Advertified.App.Contracts.Payments;

public sealed class QueuedVodaPayWebhookJob
{
    public Guid WebhookAuditId { get; set; }
    public VodaPayWebhookRequest Request { get; set; } = new();
}

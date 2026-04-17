namespace Advertified.App.Data.Entities;

public sealed class EmailDeliveryEvent
{
    public Guid Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public Guid? EmailDeliveryMessageId { get; set; }
    public string? ProviderWebhookMessageId { get; set; }
    public string? ProviderMessageId { get; set; }
    public string ProviderEventType { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public DateTime EventCreatedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ProcessingNotes { get; set; }
    public string PayloadJson { get; set; } = string.Empty;

    public EmailDeliveryMessage? EmailDeliveryMessage { get; set; }
}

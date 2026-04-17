namespace Advertified.App.Contracts.Campaigns;

public sealed class EmailDeliveryAttemptResponse
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? LatestEventType { get; set; }
    public DateTimeOffset? LatestEventAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClickedAt { get; set; }
    public DateTimeOffset? BouncedAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string? LastError { get; set; }
}

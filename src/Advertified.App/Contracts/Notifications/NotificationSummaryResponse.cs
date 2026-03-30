namespace Advertified.App.Contracts.Notifications;

public sealed class NotificationSummaryResponse
{
    public int UnreadCount { get; init; }
    public NotificationSummaryItemResponse[] Items { get; init; } = Array.Empty<NotificationSummaryItemResponse>();
}

public sealed class NotificationSummaryItemResponse
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public string Tone { get; init; } = "info";
    public bool IsRead { get; init; }
}

namespace Advertified.App.Data.Entities;

public sealed class NotificationReadReceipt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NotificationId { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}

namespace Advertified.App.Services.Abstractions;

public interface ITemplatedEmailService
{
    Task SendAsync(
        string templateName,
        string recipientEmail,
        string senderKey,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyCollection<EmailAttachment>? attachments,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken);
}

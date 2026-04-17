namespace Advertified.App.Services.Abstractions;

public interface IEmailDeliveryTrackingService
{
    Task<TrackedEmailDispatch> CreatePendingDispatchAsync(
        string providerKey,
        string templateName,
        string senderKey,
        string fromAddress,
        string recipientEmail,
        string subject,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken);

    Task MarkAcceptedAsync(
        Guid dispatchId,
        string providerMessageId,
        string? providerBroadcastId,
        CancellationToken cancellationToken);

    Task MarkArchivedAsync(
        Guid dispatchId,
        string archivePath,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid dispatchId,
        string errorMessage,
        CancellationToken cancellationToken);

    Task<EmailWebhookProcessResult> ProcessResendWebhookAsync(
        string requestPath,
        IReadOnlyDictionary<string, string> headers,
        string payload,
        CancellationToken cancellationToken);
}

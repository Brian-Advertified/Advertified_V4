namespace Advertified.App.Services.Abstractions;

public interface IPaymentAuditService
{
    Task<Guid> CreateProviderRequestAsync(
        Guid? packageOrderId,
        string provider,
        string eventType,
        string requestUrl,
        string requestHeadersJson,
        string requestBodyJson,
        string? externalReference,
        CancellationToken cancellationToken);

    Task CompleteProviderRequestAsync(
        Guid requestAuditId,
        int? responseStatusCode,
        string? responseHeadersJson,
        string? responseBodyText,
        CancellationToken cancellationToken);

    Task<Guid> CreateWebhookAsync(
        Guid? packageOrderId,
        string provider,
        string webhookPath,
        string headersJson,
        string bodyJson,
        string processedStatus,
        string? processedMessage,
        CancellationToken cancellationToken);

    Task CompleteWebhookAsync(
        Guid webhookAuditId,
        string processedStatus,
        string? processedMessage,
        CancellationToken cancellationToken);
}

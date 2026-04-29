using Advertified.App.Configuration;
using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class AdminIntegrationStatusService
{
    private const string ResendProviderKey = "resend";
    private readonly AppDbContext _db;
    private readonly ResendOptions _resendOptions;

    public AdminIntegrationStatusService(AppDbContext db, IOptions<ResendOptions> resendOptions)
    {
        _db = db;
        _resendOptions = resendOptions.Value;
    }

    public async Task<AdminIntegrationStatusResponse> GetAsync(CancellationToken cancellationToken)
    {
        var requestCount = await _db.PaymentProviderRequests.CountAsync(cancellationToken);
        var webhookCount = await _db.PaymentProviderWebhooks.CountAsync(cancellationToken);
        var lastRequestAt = await _db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
            .Select(x => (DateTime?)(x.CompletedAt ?? x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        var lastWebhookAt = await _db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var resendProvider = await _db.EmailDeliveryProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderKey == ResendProviderKey, cancellationToken);

        var emailMessages = _db.EmailDeliveryMessages
            .AsNoTracking()
            .Where(x => x.ProviderKey == ResendProviderKey);

        var pendingCount = await emailMessages.CountAsync(x => x.Status == EmailDeliveryStatuses.Pending, cancellationToken);
        var acceptedCount = await emailMessages.CountAsync(x => x.Status == EmailDeliveryStatuses.Accepted, cancellationToken);
        var deliveredCount = await emailMessages.CountAsync(x => x.Status == EmailDeliveryStatuses.Delivered, cancellationToken);
        var failedCount = await emailMessages.CountAsync(x => x.Status == EmailDeliveryStatuses.Failed, cancellationToken);
        var archivedCount = await emailMessages.CountAsync(x => x.Status == EmailDeliveryStatuses.Archived, cancellationToken);
        var lastEmailAcceptedAt = await emailMessages
            .OrderByDescending(x => x.AcceptedAt)
            .Select(x => x.AcceptedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var lastEmailDeliveredAt = await emailMessages
            .OrderByDescending(x => x.DeliveredAt)
            .Select(x => x.DeliveredAt)
            .FirstOrDefaultAsync(cancellationToken);
        var lastEmailFailedAt = await emailMessages
            .OrderByDescending(x => x.FailedAt)
            .Select(x => x.FailedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var lastEmailArchivedAt = await emailMessages
            .OrderByDescending(x => x.ArchivedAt)
            .Select(x => x.ArchivedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var lastEmailWebhookAt = await _db.EmailDeliveryWebhookAudits
            .AsNoTracking()
            .Where(x => x.ProviderKey == ResendProviderKey)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminIntegrationStatusResponse
        {
            PaymentRequestAuditCount = requestCount,
            PaymentWebhookAuditCount = webhookCount,
            LastPaymentRequestAt = lastRequestAt,
            LastPaymentWebhookAt = lastWebhookAt,
            ResendSendConfigured = ResendConfigurationInspector.IsSendConfigured(_resendOptions),
            ResendArchiveFallbackEnabled = _resendOptions.AllowLocalArchiveFallback,
            ResendWebhookEnabled = resendProvider?.WebhookEnabled == true,
            ResendWebhookSigningSecretConfigured = !string.IsNullOrWhiteSpace(resendProvider?.WebhookSigningSecret),
            ResendWebhookEndpointPath = resendProvider?.WebhookEndpointPath,
            EmailPendingCount = pendingCount,
            EmailAcceptedCount = acceptedCount,
            EmailDeliveredCount = deliveredCount,
            EmailFailedCount = failedCount,
            EmailArchivedCount = archivedCount,
            LastEmailAcceptedAt = lastEmailAcceptedAt,
            LastEmailDeliveredAt = lastEmailDeliveredAt,
            LastEmailFailedAt = lastEmailFailedAt,
            LastEmailArchivedAt = lastEmailArchivedAt,
            LastEmailWebhookAt = lastEmailWebhookAt
        };
    }
}

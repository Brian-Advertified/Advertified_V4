using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class PaymentAuditService : IPaymentAuditService
{
    private readonly AppDbContext _db;

    public PaymentAuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateProviderRequestAsync(
        Guid? packageOrderId,
        string provider,
        string eventType,
        string requestUrl,
        string requestHeadersJson,
        string requestBodyJson,
        string? externalReference,
        CancellationToken cancellationToken)
    {
        var resolvedPackageOrderId = await ResolvePackageOrderIdAsync(packageOrderId, cancellationToken);
        var audit = new PaymentProviderRequestAudit
        {
            Id = Guid.NewGuid(),
            PackageOrderId = resolvedPackageOrderId,
            Provider = provider,
            EventType = eventType,
            ExternalReference = externalReference,
            RequestUrl = requestUrl,
            RequestHeadersJson = requestHeadersJson,
            RequestBodyJson = requestBodyJson,
            CreatedAt = DateTime.UtcNow
        };

        _db.PaymentProviderRequests.Add(audit);
        await _db.SaveChangesAsync(cancellationToken);
        return audit.Id;
    }

    public async Task CompleteProviderRequestAsync(
        Guid requestAuditId,
        int? responseStatusCode,
        string? responseHeadersJson,
        string? responseBodyText,
        CancellationToken cancellationToken)
    {
        var audit = await _db.PaymentProviderRequests
            .FirstOrDefaultAsync(x => x.Id == requestAuditId, cancellationToken)
            ?? throw new InvalidOperationException("Payment provider request audit not found.");

        audit.ResponseStatusCode = responseStatusCode;
        audit.ResponseHeadersJson = responseHeadersJson;
        audit.ResponseBodyText = responseBodyText;
        audit.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateWebhookAsync(
        Guid? packageOrderId,
        string provider,
        string webhookPath,
        string headersJson,
        string bodyJson,
        string processedStatus,
        string? processedMessage,
        CancellationToken cancellationToken)
    {
        var resolvedPackageOrderId = await ResolvePackageOrderIdAsync(packageOrderId, cancellationToken);
        var audit = new PaymentProviderWebhookAudit
        {
            Id = Guid.NewGuid(),
            PackageOrderId = resolvedPackageOrderId,
            Provider = provider,
            WebhookPath = webhookPath,
            HeadersJson = headersJson,
            BodyJson = bodyJson,
            ProcessedStatus = processedStatus,
            ProcessedMessage = processedMessage,
            CreatedAt = DateTime.UtcNow
        };

        _db.PaymentProviderWebhooks.Add(audit);
        await _db.SaveChangesAsync(cancellationToken);
        return audit.Id;
    }

    public async Task CompleteWebhookAsync(
        Guid webhookAuditId,
        string processedStatus,
        string? processedMessage,
        CancellationToken cancellationToken)
    {
        var audit = await _db.PaymentProviderWebhooks
            .FirstOrDefaultAsync(x => x.Id == webhookAuditId, cancellationToken)
            ?? throw new InvalidOperationException("Payment provider webhook audit not found.");

        audit.ProcessedStatus = processedStatus;
        audit.ProcessedMessage = processedMessage;
        audit.ProcessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> ResolvePackageOrderIdAsync(Guid? packageOrderId, CancellationToken cancellationToken)
    {
        if (!packageOrderId.HasValue)
        {
            return null;
        }

        var exists = await _db.PackageOrders
            .AsNoTracking()
            .AnyAsync(x => x.Id == packageOrderId.Value, cancellationToken);

        return exists ? packageOrderId : null;
    }
}

using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/audit")]
public sealed class AdminAuditController : BaseAdminController
{
    public AdminAuditController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService)
        : base(db, currentUserAccessor, changeAuditService)
    {
    }

    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyCollection<AdminAuditEntryResponse>>> GetAudit(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var changeLogRows = await Db.ChangeAuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        var changeLogs = changeLogRows
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = FormatAuditSource(x.Scope),
                ActorName = string.IsNullOrWhiteSpace(x.ActorName) ? "System" : x.ActorName,
                ActorRole = x.ActorRole,
                EventType = x.Action,
                EntityType = x.EntityType,
                EntityLabel = x.EntityLabel,
                Context = x.Summary,
                StatusLabel = null,
                CreatedAt = x.CreatedAt,
            })
            .ToArray();

        var requestLogRows = await Db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        var requestLogs = requestLogRows
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment request",
                ActorName = "System",
                ActorRole = "integration",
                EventType = x.EventType,
                EntityType = "package_order",
                EntityLabel = x.ExternalReference,
                Context = x.RequestUrl,
                StatusLabel = x.ResponseStatusCode?.ToString(),
                CreatedAt = x.CreatedAt,
            })
            .ToArray();

        var webhookLogRows = await Db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        var webhookLogs = webhookLogRows
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment webhook",
                ActorName = "System",
                ActorRole = "integration",
                EventType = x.ProcessedStatus,
                EntityType = "package_order",
                EntityLabel = x.PackageOrderId.HasValue ? x.PackageOrderId.Value.ToString() : null,
                Context = x.WebhookPath,
                StatusLabel = x.ProcessedStatus,
                CreatedAt = x.CreatedAt,
            })
            .ToArray();

        var combined = changeLogs.Concat(requestLogs).Concat(webhookLogs)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToArray();

        return Ok(combined);
    }
}

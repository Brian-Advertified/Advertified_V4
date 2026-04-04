using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/integrations")]
public sealed class AdminIntegrationsController : BaseAdminController
{
    public AdminIntegrationsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService)
        : base(db, currentUserAccessor, changeAuditService)
    {
    }

    [HttpGet("")]
    public async Task<ActionResult<AdminIntegrationStatusResponse>> GetIntegrationStatus(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var requestCount = await Db.PaymentProviderRequests.CountAsync(cancellationToken);
        var webhookCount = await Db.PaymentProviderWebhooks.CountAsync(cancellationToken);
        var lastRequestAt = await Db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
            .Select(x => (DateTime?)(x.CompletedAt ?? x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        var lastWebhookAt = await Db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new AdminIntegrationStatusResponse
        {
            PaymentRequestAuditCount = requestCount,
            PaymentWebhookAuditCount = webhookCount,
            LastPaymentRequestAt = lastRequestAt,
            LastPaymentWebhookAt = lastWebhookAt,
        });
    }
}

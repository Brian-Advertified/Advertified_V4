using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAdminDashboardService _adminDashboardService;
    private readonly IAdminMutationService _adminMutationService;

    public AdminController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAdminDashboardService adminDashboardService,
        IAdminMutationService adminMutationService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _adminDashboardService = adminDashboardService;
        _adminMutationService = adminMutationService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        return Ok(await _adminDashboardService.GetDashboardAsync(cancellationToken));
    }

    [HttpPost("outlets")]
    public async Task<ActionResult<AdminOutletMutationResponse>> CreateOutlet([FromBody] CreateAdminOutletRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var result = await _adminMutationService.CreateOutletAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("outlets/{code}")]
    public async Task<ActionResult<AdminOutletDetailResponse>> GetOutlet(string code, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            return Ok(await _adminMutationService.GetOutletAsync(code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("outlets/{code}")]
    public async Task<ActionResult<AdminOutletMutationResponse>> UpdateOutlet(string code, [FromBody] UpdateAdminOutletRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            return Ok(await _adminMutationService.UpdateOutletAsync(code, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("outlets/{code}")]
    public async Task<IActionResult> DeleteOutlet(string code, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeleteOutletAsync(code, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("outlets/{code}/pricing")]
    public async Task<ActionResult<AdminOutletPricingResponse>> GetOutletPricing(string code, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            return Ok(await _adminMutationService.GetOutletPricingAsync(code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("outlets/{code}/pricing/packages")]
    public async Task<ActionResult<object>> CreateOutletPricingPackage(string code, [FromBody] UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var id = await _adminMutationService.CreateOutletPricingPackageAsync(code, request, cancellationToken);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("outlets/{code}/pricing/packages/{packageId:guid}")]
    public async Task<IActionResult> UpdateOutletPricingPackage(string code, Guid packageId, [FromBody] UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateOutletPricingPackageAsync(code, packageId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("outlets/{code}/pricing/packages/{packageId:guid}")]
    public async Task<IActionResult> DeleteOutletPricingPackage(string code, Guid packageId, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeleteOutletPricingPackageAsync(code, packageId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("outlets/{code}/pricing/slot-rates")]
    public async Task<ActionResult<object>> CreateOutletSlotRate(string code, [FromBody] UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var id = await _adminMutationService.CreateOutletSlotRateAsync(code, request, cancellationToken);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("outlets/{code}/pricing/slot-rates/{slotRateId:guid}")]
    public async Task<IActionResult> UpdateOutletSlotRate(string code, Guid slotRateId, [FromBody] UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateOutletSlotRateAsync(code, slotRateId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("outlets/{code}/pricing/slot-rates/{slotRateId:guid}")]
    public async Task<IActionResult> DeleteOutletSlotRate(string code, Guid slotRateId, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeleteOutletSlotRateAsync(code, slotRateId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("imports/rate-card")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<AdminRateCardUploadResponse>> UploadRateCard(
        [FromForm] string channel,
        [FromForm] string? supplierOrStation,
        [FromForm] string? documentTitle,
        [FromForm] string? notes,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var result = await _adminMutationService.UploadRateCardAsync(channel, supplierOrStation, documentTitle, notes, file, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("preview-rules/{packageCode}/{tierCode}")]
    public async Task<IActionResult> UpdatePreviewRule(string packageCode, string tierCode, [FromBody] UpdateAdminPreviewRuleRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdatePreviewRuleAsync(packageCode, tierCode, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<AdminUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var users = await _db.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToArrayAsync(cancellationToken);

        return Ok(users.Select(x => new AdminUserResponse
        {
            Id = x.Id,
            FullName = x.FullName,
            Email = x.Email,
            Role = x.Role.ToString().ToLowerInvariant(),
            AccountStatus = x.AccountStatus.ToString(),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
        }).ToArray());
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyCollection<AdminAuditEntryResponse>>> GetAudit(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var requestLogs = await _db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment request",
                Provider = x.Provider,
                EventType = x.EventType,
                ExternalReference = x.ExternalReference,
                RequestUrl = x.RequestUrl,
                ResponseStatusCode = x.ResponseStatusCode,
                CreatedAt = x.CreatedAt,
            })
            .ToArrayAsync(cancellationToken);

        var webhookLogs = await _db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment webhook",
                Provider = x.Provider,
                EventType = x.ProcessedStatus,
                ExternalReference = x.PackageOrderId.HasValue ? x.PackageOrderId.Value.ToString() : null,
                RequestUrl = x.WebhookPath,
                ResponseStatusCode = null,
                CreatedAt = x.CreatedAt,
            })
            .ToArrayAsync(cancellationToken);

        var combined = requestLogs.Concat(webhookLogs)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToArray();

        return Ok(combined);
    }

    [HttpGet("integrations")]
    public async Task<ActionResult<AdminIntegrationStatusResponse>> GetIntegrationStatus(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

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

        return Ok(new AdminIntegrationStatusResponse
        {
            PaymentRequestAuditCount = requestCount,
            PaymentWebhookAuditCount = webhookCount,
            LastPaymentRequestAt = lastRequestAt,
            LastPaymentWebhookAt = lastWebhookAt,
        });
    }

    private async Task<ActionResult?> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FindAsync(new object[] { currentUserId }, cancellationToken);
        if (currentUser is null)
        {
            return NotFound();
        }

        if (currentUser.Role != UserRole.Admin)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }
}

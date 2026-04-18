using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/lead-intelligence-settings")]
public sealed class AdminLeadIntelligenceSettingsController : BaseAdminController
{
    private readonly IAdminLeadIntelligenceSettingsService _settingsService;

    public AdminLeadIntelligenceSettingsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminLeadIntelligenceSettingsService settingsService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminLeadIntelligenceSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        return Ok(await _settingsService.GetCurrentAsync(cancellationToken));
    }

    [HttpPut("scoring")]
    public async Task<IActionResult> UpdateScoring(
        [FromBody] UpdateAdminLeadScoringSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _settingsService.UpdateScoringAsync(request, cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "lead_intelligence_scoring_settings",
                "default",
                "Lead intelligence scoring settings",
                "Updated lead intelligence scoring settings.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("automation")]
    public async Task<IActionResult> UpdateAutomation(
        [FromBody] UpdateAdminLeadIntelligenceAutomationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _settingsService.UpdateAutomationAsync(request, cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "lead_intelligence_automation_settings",
                "default",
                "Lead intelligence automation settings",
                "Updated lead intelligence automation settings.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin")]
public sealed class AdminPackageConfigurationController : BaseAdminController
{
    private readonly IAdminMutationService _adminMutationService;

    public AdminPackageConfigurationController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminMutationService adminMutationService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _adminMutationService = adminMutationService;
    }

    [HttpPost("package-settings")]
    public async Task<ActionResult<object>> CreatePackageSetting(
        [FromBody] CreateAdminPackageSettingRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var id = await _adminMutationService.CreatePackageSettingAsync(request, cancellationToken);
            await WriteChangeAuditAsync("create", "package_setting", id.ToString(), request.Name, $"Created package band {request.Name}.", new { PackageSettingId = id, request.Code, request.Name }, cancellationToken);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("package-settings/{packageSettingId:guid}")]
    public async Task<IActionResult> UpdatePackageSetting(
        Guid packageSettingId,
        [FromBody] UpdateAdminPackageSettingRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdatePackageSettingAsync(packageSettingId, request, cancellationToken);
            await WriteChangeAuditAsync("update", "package_setting", packageSettingId.ToString(), request.Name, $"Updated package band {request.Name}.", new { PackageSettingId = packageSettingId, request.Code, request.Name }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("package-settings/{packageSettingId:guid}")]
    public async Task<IActionResult> DeletePackageSetting(Guid packageSettingId, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeletePackageSettingAsync(packageSettingId, cancellationToken);
            await WriteChangeAuditAsync("delete", "package_setting", packageSettingId.ToString(), packageSettingId.ToString(), $"Deleted package band {packageSettingId}.", new { PackageSettingId = packageSettingId }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("pricing-settings")]
    public async Task<IActionResult> UpdatePricingSettings(
        [FromBody] UpdateAdminPricingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdatePricingSettingsAsync(request, cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "pricing_settings",
                "default",
                "Default pricing settings",
                "Updated platform pricing settings.",
                new
                {
                    request.AiStudioReservePercent,
                    request.OohMarkupPercent,
                    request.RadioMarkupPercent,
                    request.TvMarkupPercent,
                    request.DigitalMarkupPercent
                },
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("engine-settings/{packageCode}")]
    public async Task<IActionResult> UpdateEnginePolicy(
        string packageCode,
        [FromBody] UpdateAdminEnginePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateEnginePolicyAsync(packageCode, request, cancellationToken);
            await WriteChangeAuditAsync("update", "engine_policy", packageCode, packageCode, $"Updated engine policy for {packageCode}.", request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("planning-allocation-settings")]
    public async Task<IActionResult> UpdatePlanningAllocationSettings(
        [FromBody] UpdateAdminPlanningAllocationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdatePlanningAllocationSettingsAsync(request, cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "planning_allocation_settings",
                "planning_allocation_settings",
                "Planning allocation settings",
                "Updated planning allocation budget bands and global rules.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("preview-rules/{packageCode}/{tierCode}")]
    public async Task<IActionResult> UpdatePreviewRule(
        string packageCode,
        string tierCode,
        [FromBody] UpdateAdminPreviewRuleRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdatePreviewRuleAsync(packageCode, tierCode, request, cancellationToken);
            await WriteChangeAuditAsync("update", "preview_rule", $"{packageCode}:{tierCode}", request.TierLabel, $"Updated preview rule {tierCode} for {packageCode}.", new { PackageCode = packageCode, TierCode = tierCode, request.TierLabel }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

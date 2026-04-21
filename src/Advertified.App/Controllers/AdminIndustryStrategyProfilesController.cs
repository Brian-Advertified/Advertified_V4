using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/industry-strategy-profiles")]
public sealed class AdminIndustryStrategyProfilesController : BaseAdminController
{
    private readonly IAdminIndustryStrategyProfileService _strategyProfileService;

    public AdminIndustryStrategyProfilesController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminIndustryStrategyProfileService strategyProfileService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _strategyProfileService = strategyProfileService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] Contracts.Admin.CreateAdminIndustryStrategyProfileRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _strategyProfileService.CreateAsync(request, cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "industry_strategy_profile",
                request.IndustryCode,
                request.IndustryLabel,
                $"Created industry strategy profile {request.IndustryCode}.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{industryCode}")]
    public async Task<IActionResult> Update(
        string industryCode,
        [FromBody] Contracts.Admin.UpdateAdminIndustryStrategyProfileRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        if (!string.Equals(industryCode?.Trim(), request.IndustryCode?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Route industry code must match request industry code." });
        }

        var normalizedIndustryCode = request.IndustryCode?.Trim() ?? string.Empty;

        try
        {
            await _strategyProfileService.UpdateAsync(normalizedIndustryCode, request, cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "industry_strategy_profile",
                normalizedIndustryCode,
                request.IndustryLabel,
                $"Updated industry strategy profile {normalizedIndustryCode}.",
                request,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{industryCode}")]
    public async Task<IActionResult> Delete(string industryCode, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _strategyProfileService.DeleteAsync(industryCode, cancellationToken);
            await WriteChangeAuditAsync(
                "delete",
                "industry_strategy_profile",
                industryCode,
                industryCode,
                $"Deleted industry strategy profile {industryCode}.",
                new { IndustryCode = industryCode },
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/outlets")]
public sealed class AdminOutletsController : BaseAdminController
{
    private readonly IAdminMutationService _adminMutationService;
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminOutletsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminDashboardService adminDashboardService,
        IAdminMutationService adminMutationService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _adminDashboardService = adminDashboardService;
        _adminMutationService = adminMutationService;
    }

    [HttpGet("")]
    public async Task<ActionResult<AdminOutletPageResponse>> GetOutlets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool issuesOnly = true,
        [FromQuery] string sortBy = "priority",
        CancellationToken cancellationToken = default)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        return Ok(await _adminDashboardService.GetOutletPageAsync(page, pageSize, issuesOnly, sortBy, cancellationToken));
    }

    [HttpPost("")]
    public async Task<ActionResult<AdminOutletMutationResponse>> CreateOutlet(
        [FromBody] CreateAdminOutletRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var result = await _adminMutationService.CreateOutletAsync(request, cancellationToken);
            await WriteChangeAuditAsync("create", "outlet", result.Code, result.Name, $"Created outlet {result.Name}.", new { result.Code, result.Name }, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{code}")]
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

    [HttpPut("{code}")]
    public async Task<ActionResult<AdminOutletMutationResponse>> UpdateOutlet(
        string code,
        [FromBody] UpdateAdminOutletRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var result = await _adminMutationService.UpdateOutletAsync(code, request, cancellationToken);
            await WriteChangeAuditAsync("update", "outlet", result.Code, result.Name, $"Updated outlet {result.Name}.", new { PreviousCode = code, result.Code, result.Name }, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{code}")]
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
            await WriteChangeAuditAsync("delete", "outlet", code, code, $"Deleted outlet {code}.", new { Code = code }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{code}/pricing")]
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

    [HttpPost("{code}/pricing/packages")]
    public async Task<ActionResult<object>> CreateOutletPricingPackage(
        string code,
        [FromBody] UpsertAdminOutletPricingPackageRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var id = await _adminMutationService.CreateOutletPricingPackageAsync(code, request, cancellationToken);
            await WriteChangeAuditAsync("create", "outlet_pricing_package", id.ToString(), request.PackageName, $"Created outlet pricing package {request.PackageName}.", new { OutletCode = code, request.PackageName }, cancellationToken);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{code}/pricing/packages/{packageId:guid}")]
    public async Task<IActionResult> UpdateOutletPricingPackage(
        string code,
        Guid packageId,
        [FromBody] UpsertAdminOutletPricingPackageRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateOutletPricingPackageAsync(code, packageId, request, cancellationToken);
            await WriteChangeAuditAsync("update", "outlet_pricing_package", packageId.ToString(), request.PackageName, $"Updated outlet pricing package {request.PackageName}.", new { OutletCode = code, PackageId = packageId, request.PackageName }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{code}/pricing/packages/{packageId:guid}")]
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
            await WriteChangeAuditAsync("delete", "outlet_pricing_package", packageId.ToString(), code, $"Deleted outlet pricing package {packageId} from {code}.", new { OutletCode = code, PackageId = packageId }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{code}/pricing/slot-rates")]
    public async Task<ActionResult<object>> CreateOutletSlotRate(
        string code,
        [FromBody] UpsertAdminOutletSlotRateRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var id = await _adminMutationService.CreateOutletSlotRateAsync(code, request, cancellationToken);
            await WriteChangeAuditAsync("create", "outlet_slot_rate", id.ToString(), code, $"Created slot rate for outlet {code}.", new { OutletCode = code, SlotRateId = id, request.DayGroup, request.StartTime, request.EndTime }, cancellationToken);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{code}/pricing/slot-rates/{slotRateId:guid}")]
    public async Task<IActionResult> UpdateOutletSlotRate(
        string code,
        Guid slotRateId,
        [FromBody] UpsertAdminOutletSlotRateRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateOutletSlotRateAsync(code, slotRateId, request, cancellationToken);
            await WriteChangeAuditAsync("update", "outlet_slot_rate", slotRateId.ToString(), code, $"Updated slot rate for outlet {code}.", new { OutletCode = code, SlotRateId = slotRateId, request.DayGroup, request.StartTime, request.EndTime }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{code}/pricing/slot-rates/{slotRateId:guid}")]
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
            await WriteChangeAuditAsync("delete", "outlet_slot_rate", slotRateId.ToString(), code, $"Deleted slot rate from outlet {code}.", new { OutletCode = code, SlotRateId = slotRateId }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

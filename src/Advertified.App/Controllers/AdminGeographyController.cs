using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/geography")]
public sealed class AdminGeographyController : BaseAdminController
{
    private readonly IAdminMutationService _adminMutationService;

    public AdminGeographyController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminMutationService adminMutationService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _adminMutationService = adminMutationService;
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<AdminGeographyDetailResponse>> GetGeography(string code, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            return Ok(await _adminMutationService.GetGeographyAsync(code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("")]
    public async Task<ActionResult<AdminGeographyDetailResponse>> CreateGeography(
        [FromBody] CreateAdminGeographyRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var result = await _adminMutationService.CreateGeographyAsync(request, cancellationToken);
            await WriteChangeAuditAsync("create", "geography", result.Code, result.Label, $"Created geography mapping {result.Label}.", new { result.Code, result.Label }, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{code}")]
    public async Task<ActionResult<AdminGeographyDetailResponse>> UpdateGeography(
        string code,
        [FromBody] UpdateAdminGeographyRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var result = await _adminMutationService.UpdateGeographyAsync(code, request, cancellationToken);
            await WriteChangeAuditAsync("update", "geography", result.Code, result.Label, $"Updated geography mapping {result.Label}.", new { PreviousCode = code, result.Code, result.Label }, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> DeleteGeography(string code, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeleteGeographyAsync(code, cancellationToken);
            await WriteChangeAuditAsync("delete", "geography", code, code, $"Deleted geography mapping {code}.", new { Code = code }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{code}/mappings")]
    public async Task<ActionResult<object>> CreateGeographyMapping(
        string code,
        [FromBody] UpsertAdminGeographyMappingRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var id = await _adminMutationService.CreateGeographyMappingAsync(code, request, cancellationToken);
            await WriteChangeAuditAsync("create", "geography_mapping", id.ToString(), code, $"Created geography mapping row for {code}.", new { AreaCode = code, MappingId = id, request.Province, request.City, request.StationOrChannelName }, cancellationToken);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{code}/mappings/{mappingId:guid}")]
    public async Task<IActionResult> UpdateGeographyMapping(
        string code,
        Guid mappingId,
        [FromBody] UpsertAdminGeographyMappingRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateGeographyMappingAsync(code, mappingId, request, cancellationToken);
            await WriteChangeAuditAsync("update", "geography_mapping", mappingId.ToString(), code, $"Updated geography mapping row for {code}.", new { AreaCode = code, MappingId = mappingId, request.Province, request.City, request.StationOrChannelName }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{code}/mappings/{mappingId:guid}")]
    public async Task<IActionResult> DeleteGeographyMapping(string code, Guid mappingId, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeleteGeographyMappingAsync(code, mappingId, cancellationToken);
            await WriteChangeAuditAsync("delete", "geography_mapping", mappingId.ToString(), code, $"Deleted geography mapping row from {code}.", new { AreaCode = code, MappingId = mappingId }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

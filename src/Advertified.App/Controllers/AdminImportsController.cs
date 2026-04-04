using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/imports")]
public sealed class AdminImportsController : BaseAdminController
{
    private readonly IAdminMutationService _adminMutationService;

    public AdminImportsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminMutationService adminMutationService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _adminMutationService = adminMutationService;
    }

    [HttpPost("rate-card")]
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
            await WriteChangeAuditAsync("create", "rate_card_import", result.SourceFile, result.DocumentTitle, $"Uploaded rate card {result.DocumentTitle}.", new { result.SourceFile, result.Channel, result.SupplierOrStation }, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("rate-card/{sourceFile}")]
    public async Task<IActionResult> UpdateRateCard(
        string sourceFile,
        [FromBody] UpdateAdminRateCardRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.UpdateRateCardAsync(sourceFile, request, cancellationToken);
            await WriteChangeAuditAsync("update", "rate_card_import", sourceFile, request.DocumentTitle ?? sourceFile, $"Updated rate card metadata for {sourceFile}.", new { SourceFile = sourceFile, request.Channel, request.SupplierOrStation, request.DocumentTitle }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("rate-card/{sourceFile}")]
    public async Task<IActionResult> DeleteRateCard(string sourceFile, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await _adminMutationService.DeleteRateCardAsync(sourceFile, cancellationToken);
            await WriteChangeAuditAsync("delete", "rate_card_import", sourceFile, sourceFile, $"Deleted rate card import {sourceFile}.", new { SourceFile = sourceFile }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

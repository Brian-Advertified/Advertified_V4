using Advertified.App.Contracts.Admin;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    private static readonly string[] AllowedVoicePackPricingTiers = { "standard", "premium", "exclusive" };
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAdminDashboardService _adminDashboardService;
    private readonly IAdminMutationService _adminMutationService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ICreativeCampaignOrchestrator _creativeCampaignOrchestrator;
    private readonly IAssetJobQueue _assetJobQueue;

    public AdminController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAdminDashboardService adminDashboardService,
        IAdminMutationService adminMutationService,
        IChangeAuditService changeAuditService,
        IPasswordHashingService passwordHashingService,
        ICreativeCampaignOrchestrator creativeCampaignOrchestrator,
        IAssetJobQueue assetJobQueue)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _adminDashboardService = adminDashboardService;
        _adminMutationService = adminMutationService;
        _changeAuditService = changeAuditService;
        _passwordHashingService = passwordHashingService;
        _creativeCampaignOrchestrator = creativeCampaignOrchestrator;
        _assetJobQueue = assetJobQueue;
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

    [HttpGet("ai/cost-reports/monthly")]
    public async Task<ActionResult<IReadOnlyList<AdminAiMonthlyCostReportRow>>> GetAiMonthlyCostReport(
        [FromQuery] int months = 6,
        CancellationToken cancellationToken = default)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var safeMonths = Math.Clamp(months, 1, 24);
        var earliestMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(safeMonths - 1));

        var rows = await _db.AiUsageLogs
            .AsNoTracking()
            .Where(item => item.CreatedAt >= earliestMonth)
            .GroupBy(item => new { item.CreatedAt.Year, item.CreatedAt.Month, Provider = item.Provider ?? "Unknown" })
            .Select(group => new AdminAiMonthlyCostReportRow
            {
                Month = $"{group.Key.Year:D4}-{group.Key.Month:D2}",
                Provider = group.Key.Provider,
                TotalEstimatedCostZar = group.Sum(item => item.EstimatedCostZar),
                TotalActualCostZar = group.Sum(item => item.ActualCostZar ?? item.EstimatedCostZar),
                CompletedJobs = group.Count(item => item.Status == "completed"),
                FailedJobs = group.Count(item => item.Status == "failed"),
                RejectedJobs = group.Count(item => item.Status == "rejected")
            })
            .OrderByDescending(item => item.Month)
            .ThenBy(item => item.Provider)
            .ToArrayAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpPost("ai/jobs/creative/{jobId:guid}/replay")]
    public async Task<ActionResult<AdminAiReplayResponse>> ReplayCreativeDeadLetterJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var failedJob = await _db.AiCreativeJobStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.JobId == jobId, cancellationToken);
            if (failedJob is null || !string.Equals(failedJob.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Creative job is not in failed state.");
            }

            var replayStatus = await _creativeCampaignOrchestrator.QueueGenerationAsync(
                new GenerateCampaignCreativesCommand(
                    failedJob.CampaignId,
                    PromptOverride: null,
                    PersistOutputs: true,
                    IdempotencyKey: $"replay-{failedJob.JobId:D}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"),
                cancellationToken);

            return Ok(new AdminAiReplayResponse
            {
                ReplayedFromJobId = failedJob.JobId,
                NewJobId = replayStatus.JobId,
                CampaignId = replayStatus.CampaignId,
                Pipeline = "creative",
                Status = replayStatus.Status,
                QueuedAt = replayStatus.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("ai/jobs/assets/{jobId:guid}/replay")]
    public async Task<ActionResult<AdminAiReplayResponse>> ReplayAssetDeadLetterJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var failedJob = await _db.AiAssetJobs
            .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (failedJob is null || !string.Equals(failedJob.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Asset job is not in failed state." });
        }

        failedJob.Status = "queued";
        failedJob.Error = null;
        failedJob.LastFailure = null;
        failedJob.UpdatedAt = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _assetJobQueue.EnqueueAsync(failedJob.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StatusCode(500, new { message = $"Failed to replay asset job {failedJob.Id}." });
        }

        return Ok(new AdminAiReplayResponse
        {
            ReplayedFromJobId = failedJob.Id,
            NewJobId = failedJob.Id,
            CampaignId = failedJob.CampaignId,
            Pipeline = "asset",
            Status = "queued",
            QueuedAt = new DateTimeOffset(failedJob.UpdatedAt, TimeSpan.Zero)
        });
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
            await WriteChangeAuditAsync("create", "outlet", result.Code, result.Name, $"Created outlet {result.Name}.", new { result.Code, result.Name }, cancellationToken);
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
            var result = await _adminMutationService.UpdateOutletAsync(code, request, cancellationToken);
            await WriteChangeAuditAsync("update", "outlet", result.Code, result.Name, $"Updated outlet {result.Name}.", new { PreviousCode = code, result.Code, result.Name }, cancellationToken);
            return Ok(result);
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
            await WriteChangeAuditAsync("delete", "outlet", code, code, $"Deleted outlet {code}.", new { Code = code }, cancellationToken);
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
            await WriteChangeAuditAsync("create", "outlet_pricing_package", id.ToString(), request.PackageName, $"Created outlet pricing package {request.PackageName}.", new { OutletCode = code, request.PackageName }, cancellationToken);
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
            await WriteChangeAuditAsync("update", "outlet_pricing_package", packageId.ToString(), request.PackageName, $"Updated outlet pricing package {request.PackageName}.", new { OutletCode = code, PackageId = packageId, request.PackageName }, cancellationToken);
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
            await WriteChangeAuditAsync("delete", "outlet_pricing_package", packageId.ToString(), code, $"Deleted outlet pricing package {packageId} from {code}.", new { OutletCode = code, PackageId = packageId }, cancellationToken);
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
            await WriteChangeAuditAsync("create", "outlet_slot_rate", id.ToString(), code, $"Created slot rate for outlet {code}.", new { OutletCode = code, SlotRateId = id, request.DayGroup, request.StartTime, request.EndTime }, cancellationToken);
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
            await WriteChangeAuditAsync("update", "outlet_slot_rate", slotRateId.ToString(), code, $"Updated slot rate for outlet {code}.", new { OutletCode = code, SlotRateId = slotRateId, request.DayGroup, request.StartTime, request.EndTime }, cancellationToken);
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
            await WriteChangeAuditAsync("delete", "outlet_slot_rate", slotRateId.ToString(), code, $"Deleted slot rate from outlet {code}.", new { OutletCode = code, SlotRateId = slotRateId }, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("geography/{code}")]
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

    [HttpPost("geography")]
    public async Task<ActionResult<AdminGeographyDetailResponse>> CreateGeography([FromBody] CreateAdminGeographyRequest request, CancellationToken cancellationToken)
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

    [HttpPut("geography/{code}")]
    public async Task<ActionResult<AdminGeographyDetailResponse>> UpdateGeography(string code, [FromBody] UpdateAdminGeographyRequest request, CancellationToken cancellationToken)
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

    [HttpDelete("geography/{code}")]
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

    [HttpPost("geography/{code}/mappings")]
    public async Task<ActionResult<object>> CreateGeographyMapping(string code, [FromBody] UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken)
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

    [HttpPut("geography/{code}/mappings/{mappingId:guid}")]
    public async Task<IActionResult> UpdateGeographyMapping(string code, Guid mappingId, [FromBody] UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken)
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

    [HttpDelete("geography/{code}/mappings/{mappingId:guid}")]
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
            await WriteChangeAuditAsync("create", "rate_card_import", result.SourceFile, result.DocumentTitle, $"Uploaded rate card {result.DocumentTitle}.", new { result.SourceFile, result.Channel, result.SupplierOrStation }, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("imports/rate-card/{sourceFile}")]
    public async Task<IActionResult> UpdateRateCard(string sourceFile, [FromBody] UpdateAdminRateCardRequest request, CancellationToken cancellationToken)
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

    [HttpDelete("imports/rate-card/{sourceFile}")]
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

    [HttpPost("package-settings")]
    public async Task<ActionResult<object>> CreatePackageSetting([FromBody] CreateAdminPackageSettingRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> UpdatePackageSetting(Guid packageSettingId, [FromBody] UpdateAdminPackageSettingRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> UpdatePricingSettings([FromBody] UpdateAdminPricingSettingsRequest request, CancellationToken cancellationToken)
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
                    request.TvMarkupPercent
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
    public async Task<IActionResult> UpdateEnginePolicy(string packageCode, [FromBody] UpdateAdminEnginePolicyRequest request, CancellationToken cancellationToken)
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
            await WriteChangeAuditAsync("update", "preview_rule", $"{packageCode}:{tierCode}", request.TierLabel, $"Updated preview rule {tierCode} for {packageCode}.", new { PackageCode = packageCode, TierCode = tierCode, request.TierLabel }, cancellationToken);
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

        var assignments = await _db.AgentAreaAssignments.AsNoTracking().ToArrayAsync(cancellationToken);
        var areaLabelsByCode = await GetAreaLabelsByCodeAsync(cancellationToken);

        return Ok(users.Select(user => MapAdminUser(user, assignments, areaLabelsByCode)).ToArray());
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserResponse>> CreateUser([FromBody] CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var user = await BuildUserAsync(request.FullName, request.Email, request.Phone, request.Password, request.Role, request.AccountStatus, request.IsSaCitizen, request.EmailVerified, request.PhoneVerified, cancellationToken);
            _db.UserAccounts.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
            await SyncAgentAreaAssignmentsAsync(user.Id, user.Role, request.AssignedAreaCodes, cancellationToken);
            await WriteChangeAuditAsync("create", "user_account", user.Id.ToString(), user.FullName, $"Created user account {user.FullName}.", new { user.Email, request.Role, request.AccountStatus, request.AssignedAreaCodes }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(await BuildAdminUserResponseAsync(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUser(Guid id, [FromBody] UpdateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var actorUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (user is null)
            {
                return NotFound();
            }

            var normalizedEmail = NormalizeEmail(request.Email);
            var duplicateExists = await _db.UserAccounts.AnyAsync(x => x.Id != id && x.Email == normalizedEmail, cancellationToken);
            if (duplicateExists)
            {
                throw new InvalidOperationException("A user with this email address already exists.");
            }

            user.FullName = RequireValue(request.FullName, "Full name");
            user.Email = normalizedEmail;
            user.Phone = RequireValue(request.Phone, "Phone");
            user.Role = ParseUserRole(request.Role);
            user.AccountStatus = ParseAccountStatus(request.AccountStatus);
            user.IsSaCitizen = request.IsSaCitizen;
            user.EmailVerified = request.EmailVerified;
            user.PhoneVerified = request.PhoneVerified;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = _passwordHashingService.HashPassword(user, request.Password);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await SyncAgentAreaAssignmentsAsync(user.Id, user.Role, request.AssignedAreaCodes, cancellationToken);
            await WriteChangeAuditAsync(actorUserId, "update", "user_account", user.Id.ToString(), user.FullName, $"Updated user account {user.FullName}.", new { user.Email, request.Role, request.AccountStatus, request.AssignedAreaCodes }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(await BuildAdminUserResponseAsync(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var user = await _db.UserAccounts
            .Include(x => x.Campaigns)
            .Include(x => x.PackageOrders)
            .Include(x => x.BusinessProfile)
            .Include(x => x.IdentityProfile)
            .Include(x => x.EmailVerificationTokens)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var hasLinkedRecommendations = await _db.CampaignRecommendations.AnyAsync(x => x.CreatedByUserId == id, cancellationToken);
        if (user.Campaigns.Count > 0 || user.PackageOrders.Count > 0 || hasLinkedRecommendations)
        {
            return BadRequest(new { message = "This user already has linked campaigns, recommendations, or orders and cannot be deleted." });
        }

        _db.EmailVerificationTokens.RemoveRange(user.EmailVerificationTokens);
        if (user.BusinessProfile is not null)
        {
            _db.BusinessProfiles.Remove(user.BusinessProfile);
        }

        if (user.IdentityProfile is not null)
        {
            _db.IdentityProfiles.Remove(user.IdentityProfile);
        }

        _db.UserAccounts.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync("delete", "user_account", user.Id.ToString(), user.FullName, $"Deleted user account {user.FullName}.", new { user.Email }, cancellationToken);
        return NoContent();
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyCollection<AdminAuditEntryResponse>>> GetAudit(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var changeLogRows = await _db.ChangeAuditLogs
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

        var requestLogRows = await _db.PaymentProviderRequests
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

        var webhookLogRows = await _db.PaymentProviderWebhooks
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

    [HttpGet("ai/voices")]
    public async Task<ActionResult<IReadOnlyList<AdminAiVoiceProfileResponse>>> GetAiVoiceProfiles(
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        var rows = await _db.AiVoiceProfiles
            .AsNoTracking()
            .Where(item => item.Provider == normalizedProvider)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Label)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(MapAdminAiVoiceProfile).ToArray());
    }

    [HttpPost("ai/voices")]
    public async Task<ActionResult<AdminAiVoiceProfileResponse>> CreateAiVoiceProfile(
        [FromBody] UpsertAdminAiVoiceProfileRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var label = RequireValue(request.Label, "Label");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");

            var exists = await _db.AiVoiceProfiles.AnyAsync(
                item => item.Provider == provider && item.Label == label,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A voice profile with this label already exists for the provider.");
            }

            var now = DateTime.UtcNow;
            var row = new AiVoiceProfile
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Label = label,
                VoiceId = voiceId,
                Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim(),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.AiVoiceProfiles.Add(row);
            await _db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "ai_voice_profile",
                row.Id.ToString(),
                row.Label,
                $"Created AI voice profile {row.Label}.",
                new { row.Provider, row.Label, row.VoiceId, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceProfile(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("ai/voices/{id:guid}")]
    public async Task<ActionResult<AdminAiVoiceProfileResponse>> UpdateAiVoiceProfile(
        Guid id,
        [FromBody] UpsertAdminAiVoiceProfileRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var row = await _db.AiVoiceProfiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null)
            {
                return NotFound();
            }

            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var label = RequireValue(request.Label, "Label");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");

            var duplicate = await _db.AiVoiceProfiles.AnyAsync(
                item => item.Id != id && item.Provider == provider && item.Label == label,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A voice profile with this label already exists for the provider.");
            }

            row.Provider = provider;
            row.Label = label;
            row.VoiceId = voiceId;
            row.Language = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language.Trim();
            row.IsActive = request.IsActive;
            row.SortOrder = request.SortOrder;
            row.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "ai_voice_profile",
                row.Id.ToString(),
                row.Label,
                $"Updated AI voice profile {row.Label}.",
                new { row.Provider, row.Label, row.VoiceId, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceProfile(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("ai/voices/{id:guid}")]
    public async Task<IActionResult> DeleteAiVoiceProfile(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var row = await _db.AiVoiceProfiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        _db.AiVoiceProfiles.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete",
            "ai_voice_profile",
            id.ToString(),
            row.Label,
            $"Deleted AI voice profile {row.Label}.",
            new { row.Provider, row.Label },
            cancellationToken);
        return NoContent();
    }

    [HttpGet("ai/voice-packs")]
    public async Task<ActionResult<IReadOnlyList<AdminAiVoicePackResponse>>> GetAiVoicePacks(
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        var rows = await _db.AiVoicePacks
            .AsNoTracking()
            .Where(item => item.Provider == normalizedProvider)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(MapAdminAiVoicePack).ToArray());
    }

    [HttpPost("ai/voice-packs")]
    public async Task<ActionResult<AdminAiVoicePackResponse>> CreateAiVoicePack(
        [FromBody] UpsertAdminAiVoicePackRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var name = RequireValue(request.Name, "Name");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var pricingTier = NormalizePricingTier(request.PricingTier);
            if (request.IsClientSpecific && !request.ClientUserId.HasValue)
            {
                throw new InvalidOperationException("Client user id is required for client-specific voice packs.");
            }

            var exists = await _db.AiVoicePacks.AnyAsync(
                item => item.Provider == provider && item.Name == name,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A voice pack with this name already exists for the provider.");
            }

            var now = DateTime.UtcNow;
            var row = new AiVoicePack
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                Name = name,
                Accent = TrimOrNull(request.Accent),
                Language = TrimOrNull(request.Language),
                Tone = TrimOrNull(request.Tone),
                Persona = TrimOrNull(request.Persona),
                UseCasesJson = SerializeList(request.UseCases),
                VoiceId = voiceId,
                SampleAudioUrl = TrimOrNull(request.SampleAudioUrl),
                PromptTemplate = promptTemplate,
                PricingTier = pricingTier,
                IsClientSpecific = request.IsClientSpecific,
                ClientUserId = request.ClientUserId,
                IsClonedVoice = request.IsClonedVoice,
                AudienceTagsJson = SerializeList(request.AudienceTags),
                ObjectiveTagsJson = SerializeList(request.ObjectiveTags),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.AiVoicePacks.Add(row);
            await _db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "ai_voice_pack",
                row.Id.ToString(),
                row.Name,
                $"Created AI voice pack {row.Name}.",
                new { row.Provider, row.Name, row.PricingTier, row.IsClientSpecific, row.ClientUserId, row.IsClonedVoice, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoicePack(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("ai/voice-packs/{id:guid}")]
    public async Task<ActionResult<AdminAiVoicePackResponse>> UpdateAiVoicePack(
        Guid id,
        [FromBody] UpsertAdminAiVoicePackRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var row = await _db.AiVoicePacks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null)
            {
                return NotFound();
            }

            var provider = string.IsNullOrWhiteSpace(request.Provider) ? "ElevenLabs" : request.Provider.Trim();
            var name = RequireValue(request.Name, "Name");
            var voiceId = RequireValue(request.VoiceId, "Voice ID");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var pricingTier = NormalizePricingTier(request.PricingTier);
            if (request.IsClientSpecific && !request.ClientUserId.HasValue)
            {
                throw new InvalidOperationException("Client user id is required for client-specific voice packs.");
            }

            var duplicate = await _db.AiVoicePacks.AnyAsync(
                item => item.Id != id && item.Provider == provider && item.Name == name,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A voice pack with this name already exists for the provider.");
            }

            row.Provider = provider;
            row.Name = name;
            row.Accent = TrimOrNull(request.Accent);
            row.Language = TrimOrNull(request.Language);
            row.Tone = TrimOrNull(request.Tone);
            row.Persona = TrimOrNull(request.Persona);
            row.UseCasesJson = SerializeList(request.UseCases);
            row.VoiceId = voiceId;
            row.SampleAudioUrl = TrimOrNull(request.SampleAudioUrl);
            row.PromptTemplate = promptTemplate;
            row.PricingTier = pricingTier;
            row.IsClientSpecific = request.IsClientSpecific;
            row.ClientUserId = request.ClientUserId;
            row.IsClonedVoice = request.IsClonedVoice;
            row.AudienceTagsJson = SerializeList(request.AudienceTags);
            row.ObjectiveTagsJson = SerializeList(request.ObjectiveTags);
            row.IsActive = request.IsActive;
            row.SortOrder = request.SortOrder;
            row.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "ai_voice_pack",
                row.Id.ToString(),
                row.Name,
                $"Updated AI voice pack {row.Name}.",
                new { row.Provider, row.Name, row.PricingTier, row.IsClientSpecific, row.ClientUserId, row.IsClonedVoice, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoicePack(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("ai/voice-packs/{id:guid}")]
    public async Task<IActionResult> DeleteAiVoicePack(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var row = await _db.AiVoicePacks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        _db.AiVoicePacks.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete",
            "ai_voice_pack",
            id.ToString(),
            row.Name,
            $"Deleted AI voice pack {row.Name}.",
            new { row.Provider, row.Name },
            cancellationToken);
        return NoContent();
    }

    [HttpGet("ai/voice-templates")]
    public async Task<ActionResult<IReadOnlyList<AdminAiVoiceTemplateResponse>>> GetAiVoiceTemplates(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var rows = await _db.AiVoicePromptTemplates
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.TemplateNumber)
            .ToArrayAsync(cancellationToken);

        return Ok(rows.Select(MapAdminAiVoiceTemplate).ToArray());
    }

    [HttpPost("ai/voice-templates")]
    public async Task<ActionResult<AdminAiVoiceTemplateResponse>> CreateAiVoiceTemplate(
        [FromBody] UpsertAdminAiVoiceTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            if (request.TemplateNumber <= 0)
            {
                throw new InvalidOperationException("Template number must be greater than zero.");
            }

            var category = RequireValue(request.Category, "Category");
            var name = RequireValue(request.Name, "Name");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var primaryVoicePackName = RequireValue(request.PrimaryVoicePackName, "Primary voice pack name");

            var exists = await _db.AiVoicePromptTemplates.AnyAsync(
                item => item.TemplateNumber == request.TemplateNumber,
                cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("A template with this template number already exists.");
            }

            var now = DateTime.UtcNow;
            var row = new AiVoicePromptTemplate
            {
                Id = Guid.NewGuid(),
                TemplateNumber = request.TemplateNumber,
                Category = category,
                Name = name,
                PromptTemplate = promptTemplate,
                PrimaryVoicePackName = primaryVoicePackName,
                FallbackVoicePackNamesJson = SerializeList(request.FallbackVoicePackNames),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.AiVoicePromptTemplates.Add(row);
            await _db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "create",
                "ai_voice_prompt_template",
                row.Id.ToString(),
                row.Name,
                $"Created AI voice template #{row.TemplateNumber} {row.Name}.",
                new { row.TemplateNumber, row.Category, row.Name, row.PrimaryVoicePackName, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceTemplate(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("ai/voice-templates/{id:guid}")]
    public async Task<ActionResult<AdminAiVoiceTemplateResponse>> UpdateAiVoiceTemplate(
        Guid id,
        [FromBody] UpsertAdminAiVoiceTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var row = await _db.AiVoicePromptTemplates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null)
            {
                return NotFound();
            }

            if (request.TemplateNumber <= 0)
            {
                throw new InvalidOperationException("Template number must be greater than zero.");
            }

            var category = RequireValue(request.Category, "Category");
            var name = RequireValue(request.Name, "Name");
            var promptTemplate = RequireValue(request.PromptTemplate, "Prompt template");
            var primaryVoicePackName = RequireValue(request.PrimaryVoicePackName, "Primary voice pack name");

            var duplicate = await _db.AiVoicePromptTemplates.AnyAsync(
                item => item.Id != id && item.TemplateNumber == request.TemplateNumber,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A template with this template number already exists.");
            }

            row.TemplateNumber = request.TemplateNumber;
            row.Category = category;
            row.Name = name;
            row.PromptTemplate = promptTemplate;
            row.PrimaryVoicePackName = primaryVoicePackName;
            row.FallbackVoicePackNamesJson = SerializeList(request.FallbackVoicePackNames);
            row.IsActive = request.IsActive;
            row.SortOrder = request.SortOrder;
            row.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await WriteChangeAuditAsync(
                "update",
                "ai_voice_prompt_template",
                row.Id.ToString(),
                row.Name,
                $"Updated AI voice template #{row.TemplateNumber} {row.Name}.",
                new { row.TemplateNumber, row.Category, row.Name, row.PrimaryVoicePackName, row.IsActive, row.SortOrder },
                cancellationToken);

            return Ok(MapAdminAiVoiceTemplate(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("ai/voice-templates/{id:guid}")]
    public async Task<IActionResult> DeleteAiVoiceTemplate(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var row = await _db.AiVoicePromptTemplates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        _db.AiVoicePromptTemplates.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete",
            "ai_voice_prompt_template",
            id.ToString(),
            row.Name,
            $"Deleted AI voice template #{row.TemplateNumber} {row.Name}.",
            new { row.TemplateNumber, row.Category, row.Name },
            cancellationToken);
        return NoContent();
    }

    private static AdminAiVoiceProfileResponse MapAdminAiVoiceProfile(AiVoiceProfile row)
    {
        return new AdminAiVoiceProfileResponse
        {
            Id = row.Id,
            Provider = row.Provider,
            Label = row.Label,
            VoiceId = row.VoiceId,
            Language = row.Language,
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    private static AdminAiVoicePackResponse MapAdminAiVoicePack(AiVoicePack row)
    {
        return new AdminAiVoicePackResponse
        {
            Id = row.Id,
            Provider = row.Provider,
            Name = row.Name,
            Accent = row.Accent,
            Language = row.Language,
            Tone = row.Tone,
            Persona = row.Persona,
            UseCases = DeserializeList(row.UseCasesJson),
            VoiceId = row.VoiceId,
            SampleAudioUrl = row.SampleAudioUrl,
            PromptTemplate = row.PromptTemplate,
            PricingTier = row.PricingTier,
            IsClientSpecific = row.IsClientSpecific,
            ClientUserId = row.ClientUserId,
            IsClonedVoice = row.IsClonedVoice,
            AudienceTags = DeserializeList(row.AudienceTagsJson),
            ObjectiveTags = DeserializeList(row.ObjectiveTagsJson),
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    private static AdminAiVoiceTemplateResponse MapAdminAiVoiceTemplate(AiVoicePromptTemplate row)
    {
        return new AdminAiVoiceTemplateResponse
        {
            Id = row.Id,
            TemplateNumber = row.TemplateNumber,
            Category = row.Category,
            Name = row.Name,
            PromptTemplate = row.PromptTemplate,
            PrimaryVoicePackName = row.PrimaryVoicePackName,
            FallbackVoicePackNames = DeserializeList(row.FallbackVoicePackNamesJson),
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    private static string SerializeList(IEnumerable<string>? values)
    {
        var normalized = (values ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return JsonSerializer.Serialize(normalized);
    }

    private static string[] DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string NormalizePricingTier(string? value)
    {
        var normalized = (value ?? "standard").Trim().ToLowerInvariant();
        if (!AllowedVoicePackPricingTiers.Contains(normalized))
        {
            throw new InvalidOperationException("Pricing tier is invalid.");
        }

        return normalized;
    }

    private static string? TrimOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<ActionResult?> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }

        var currentUser = await _db.UserAccounts.FindAsync(new object[] { currentUserId }, cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (currentUser.Role != UserRole.Admin)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private static string FormatAuditSource(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return "System change";
        }

        var normalizedScope = scope.Trim().ToLowerInvariant();
        return $"{char.ToUpperInvariant(normalizedScope[0])}{normalizedScope.Substring(1)} change";
    }

    private async Task WriteChangeAuditAsync(
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _changeAuditService.WriteAsync(currentUserId, "admin", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private Task WriteChangeAuditAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        return _changeAuditService.WriteAsync(actorUserId, "admin", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private async Task<AdminUserResponse> BuildAdminUserResponseAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var assignments = await _db.AgentAreaAssignments
            .AsNoTracking()
            .Where(x => x.AgentUserId == user.Id)
            .ToArrayAsync(cancellationToken);
        var areaLabelsByCode = await GetAreaLabelsByCodeAsync(cancellationToken);
        return MapAdminUser(user, assignments, areaLabelsByCode);
    }

    private async Task<Dictionary<string, string>> GetAreaLabelsByCodeAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Database
            .SqlQueryRaw<AreaLabelLookup>("select cluster_code as Code, display_name as Label from package_area_profiles where is_active = true;")
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(x => x.Code, x => x.Label, StringComparer.OrdinalIgnoreCase);
    }

    private async Task SyncAgentAreaAssignmentsAsync(Guid userId, UserRole role, IReadOnlyList<string> areaCodes, CancellationToken cancellationToken)
    {
        areaCodes ??= Array.Empty<string>();

        var existingAssignments = await _db.AgentAreaAssignments
            .Where(x => x.AgentUserId == userId)
            .ToArrayAsync(cancellationToken);

        if (role != UserRole.Agent)
        {
            if (existingAssignments.Length > 0)
            {
                _db.AgentAreaAssignments.RemoveRange(existingAssignments);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var normalizedCodes = areaCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var validAreaCodes = await _db.Database
            .SqlQueryRaw<AreaCodeLookup>("select cluster_code as \"Code\" from package_area_profiles where is_active = true")
            .Select(x => x.Code)
            .ToArrayAsync(cancellationToken);

        var invalidCodes = normalizedCodes
            .Where(code => !validAreaCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (invalidCodes.Length > 0)
        {
            throw new InvalidOperationException($"Unknown area code(s): {string.Join(", ", invalidCodes)}.");
        }

        var conflictingAssignments = await _db.AgentAreaAssignments
            .AsNoTracking()
            .Where(x => x.AgentUserId != userId && normalizedCodes.Contains(x.AreaCode))
            .ToArrayAsync(cancellationToken);

        if (conflictingAssignments.Length > 0)
        {
            throw new InvalidOperationException($"These areas are already assigned to another agent: {string.Join(", ", conflictingAssignments.Select(x => x.AreaCode).OrderBy(x => x))}.");
        }

        var toRemove = existingAssignments
            .Where(x => !normalizedCodes.Contains(x.AreaCode, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (toRemove.Length > 0)
        {
            _db.AgentAreaAssignments.RemoveRange(toRemove);
        }

        var existingCodes = existingAssignments
            .Select(x => x.AreaCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var areaCode in normalizedCodes)
        {
            if (existingCodes.Contains(areaCode))
            {
                continue;
            }

            _db.AgentAreaAssignments.Add(new AgentAreaAssignment
            {
                Id = Guid.NewGuid(),
                AgentUserId = userId,
                AreaCode = areaCode,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserAccount> BuildUserAsync(
        string fullName,
        string email,
        string phone,
        string password,
        string role,
        string accountStatus,
        bool isSaCitizen,
        bool emailVerified,
        bool phoneVerified,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var duplicateExists = await _db.UserAccounts.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (duplicateExists)
        {
            throw new InvalidOperationException("A user with this email address already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            FullName = RequireValue(fullName, "Full name"),
            Email = normalizedEmail,
            Phone = RequireValue(phone, "Phone"),
            Role = ParseUserRole(role),
            AccountStatus = ParseAccountStatus(accountStatus),
            IsSaCitizen = isSaCitizen,
            EmailVerified = emailVerified,
            PhoneVerified = phoneVerified,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = _passwordHashingService.HashPassword(user, password);
        return user;
    }

    private static AdminUserResponse MapAdminUser(UserAccount user, IEnumerable<AgentAreaAssignment> assignments, IReadOnlyDictionary<string, string> areaLabelsByCode)
    {
        var assignedAreaCodes = assignments
            .Where(x => x.AgentUserId == user.Id)
            .Select(x => x.AreaCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = FormatUserRole(user.Role),
            AccountStatus = user.AccountStatus.ToString(),
            IsSaCitizen = user.IsSaCitizen,
            EmailVerified = user.EmailVerified,
            PhoneVerified = user.PhoneVerified,
            AssignedAreaCodes = assignedAreaCodes,
            AssignedAreaLabels = assignedAreaCodes.Select(code => areaLabelsByCode.TryGetValue(code, out var label) ? label : code).ToArray(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };
    }

    private sealed class AreaCodeLookup
    {
        public string Code { get; set; } = string.Empty;
    }

    private sealed class AreaLabelLookup
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    private static UserRole ParseUserRole(string role)
    {
        var normalizedRole = NormalizeEnumToken(role);
        if (Enum.TryParse<UserRole>(normalizedRole, true, out var parsedRole))
        {
            return parsedRole;
        }

        throw new InvalidOperationException("Role is invalid.");
    }

    private static AccountStatus ParseAccountStatus(string accountStatus)
    {
        var normalizedStatus = NormalizeEnumToken(accountStatus);
        if (Enum.TryParse<AccountStatus>(normalizedStatus, true, out var parsedStatus))
        {
            return parsedStatus;
        }

        throw new InvalidOperationException("Account status is invalid.");
    }

    private static string NormalizeEmail(string email)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Email is required.");
        }

        return normalizedEmail;
    }

    private static string RequireValue(string value, string label)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        return normalized;
    }

    private static string NormalizeEnumToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value
            .Trim()
            .Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        return string.Concat(parts.Select(static part =>
            char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));
    }

    private static string FormatUserRole(UserRole role)
    {
        return role switch
        {
            UserRole.CreativeDirector => "creative_director",
            UserRole.Agent => "agent",
            UserRole.Admin => "admin",
            _ => "client"
        };
    }

    public sealed class AdminAiMonthlyCostReportRow
    {
        public string Month { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public decimal TotalEstimatedCostZar { get; set; }
        public decimal TotalActualCostZar { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int RejectedJobs { get; set; }
    }

    public sealed class AdminAiReplayResponse
    {
        public Guid ReplayedFromJobId { get; set; }
        public Guid NewJobId { get; set; }
        public Guid CampaignId { get; set; }
        public string Pipeline { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset QueuedAt { get; set; }
    }

}

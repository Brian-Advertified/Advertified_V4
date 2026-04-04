using Advertified.App.AIPlatform.Application;
using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/ai")]
public sealed class AdminAiOperationsController : BaseAdminController
{
    private readonly ICreativeCampaignOrchestrator _creativeCampaignOrchestrator;
    private readonly IAssetJobQueue _assetJobQueue;

    public AdminAiOperationsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        ICreativeCampaignOrchestrator creativeCampaignOrchestrator,
        IAssetJobQueue assetJobQueue)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _creativeCampaignOrchestrator = creativeCampaignOrchestrator;
        _assetJobQueue = assetJobQueue;
    }

    [HttpGet("cost-reports/monthly")]
    public async Task<ActionResult<IReadOnlyList<AdminAiMonthlyCostReportRow>>> GetMonthlyCostReport(
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

        var rows = await Db.AiUsageLogs
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

    [HttpPost("jobs/creative/{jobId:guid}/replay")]
    public async Task<ActionResult<AdminAiReplayResponse>> ReplayCreativeJob(
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
            var failedJob = await Db.AiCreativeJobStatuses
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

    [HttpPost("jobs/assets/{jobId:guid}/replay")]
    public async Task<ActionResult<AdminAiReplayResponse>> ReplayAssetJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var failedJob = await Db.AiAssetJobs
            .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (failedJob is null || !string.Equals(failedJob.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Asset job is not in failed state." });
        }

        failedJob.Status = "queued";
        failedJob.Error = null;
        failedJob.LastFailure = null;
        failedJob.UpdatedAt = DateTime.UtcNow;

        await using var transaction = await Db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
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
}

using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform")]
public sealed class AiPlatformController : ControllerBase
{
    private readonly ICreativeCampaignOrchestrator _creativeCampaignOrchestrator;
    private readonly ICreativeJobQueue _creativeJobQueue;
    private readonly ICreativeFeedbackRegenerationService _creativeFeedbackRegenerationService;
    private readonly IAiCostControlService _aiCostControlService;
    private readonly AppDbContext _db;
    private readonly AiPlatformOptions _options;

    public AiPlatformController(
        ICreativeCampaignOrchestrator creativeCampaignOrchestrator,
        ICreativeJobQueue creativeJobQueue,
        ICreativeFeedbackRegenerationService creativeFeedbackRegenerationService,
        IAiCostControlService aiCostControlService,
        AppDbContext db,
        IOptions<AiPlatformOptions> options)
    {
        _creativeCampaignOrchestrator = creativeCampaignOrchestrator;
        _creativeJobQueue = creativeJobQueue;
        _creativeFeedbackRegenerationService = creativeFeedbackRegenerationService;
        _aiCostControlService = aiCostControlService;
        _db = db;
        _options = options.Value;
    }

    [HttpPost("jobs")]
    public async Task<ActionResult<SubmitCreativeGenerationResponse>> Submit(
        [FromBody] SubmitCreativeGenerationRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        if (request.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId is required.");
        }

        var status = await _creativeCampaignOrchestrator.QueueGenerationAsync(
            new GenerateCampaignCreativesCommand(
                request.CampaignId,
                request.PromptOverride,
                PersistOutputs: true,
                VoicePackId: request.VoicePackId,
                IdempotencyKey: string.IsNullOrWhiteSpace(request.IdempotencyKey) ? idempotencyKeyHeader : request.IdempotencyKey),
            cancellationToken);

        return Accepted(new SubmitCreativeGenerationResponse
        {
            JobId = status.JobId,
            CampaignId = status.CampaignId,
            Status = status.Status,
            QueuedAt = status.UpdatedAt
        });
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<CreativeJobStatusResponse>> GetStatus(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var status = await _creativeJobQueue.GetStatusAsync(jobId, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        return Ok(new CreativeJobStatusResponse
        {
            JobId = status.JobId,
            CampaignId = status.CampaignId,
            Status = status.Status,
            Error = status.Error,
            RetryAttemptCount = status.RetryAttemptCount,
            LastFailure = status.LastFailure,
            UpdatedAt = status.UpdatedAt
        });
    }

    [HttpPost("regenerate")]
    public async Task<IActionResult> Regenerate(
        [FromBody] RegenerateCreativeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.CreativeId == Guid.Empty || request.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("creativeId and campaignId are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Feedback))
        {
            throw new InvalidOperationException("feedback is required.");
        }

        var regenerationCount = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(
                x => x.CampaignId == request.CampaignId
                    && x.Operation == "creative_regeneration"
                    && x.Status == "completed",
                cancellationToken);
        if (regenerationCount >= Math.Max(1, _options.FreeRegenerationLimit))
        {
            throw new InvalidOperationException("Regeneration limit reached for this campaign.");
        }

        var regenerationLog = await _aiCostControlService.GuardAsync(new AiCostGuardRequest(
            CampaignId: request.CampaignId,
            Operation: "creative_regeneration",
            Provider: "OpenAI",
            EstimatedCostZar: 0m,
            CreativeId: request.CreativeId,
            Details: "Regeneration request accepted."), cancellationToken);
        if (!regenerationLog.Allowed)
        {
            throw new InvalidOperationException(regenerationLog.Message);
        }

        try
        {
            var result = await _creativeFeedbackRegenerationService.RegenerateAsync(
                new RegenerationFeedback(request.CreativeId, request.CampaignId, request.Feedback.Trim(), DateTimeOffset.UtcNow),
                cancellationToken);
            if (regenerationLog.UsageLogId.HasValue)
            {
                await _aiCostControlService.CompleteAsync(regenerationLog.UsageLogId.Value, 0m, "Regeneration completed.", cancellationToken);
            }

            return Ok(new
            {
                result.JobId,
                result.CampaignId,
                CreativeCount = result.Creatives.Count,
                AssetCount = result.Assets.Count,
                result.CompletedAt
            });
        }
        catch (Exception)
        {
            if (regenerationLog.UsageLogId.HasValue)
            {
                await _aiCostControlService.FailAsync(regenerationLog.UsageLogId.Value, "Regeneration failed.", cancellationToken);
            }

            throw;
        }
    }

    [HttpGet("campaigns/{campaignId:guid}/creatives")]
    public async Task<ActionResult<IReadOnlyList<AiPlatformCampaignCreativeItemResponse>>> GetCampaignCreatives(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var items = await _db.CampaignCreatives
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new AiPlatformCampaignCreativeItemResponse
            {
                Id = item.Id,
                CampaignId = item.CampaignId,
                Channel = item.Channel,
                Language = item.Language,
                Score = item.Score,
                CreatedAt = new DateTimeOffset(item.CreatedAt, TimeSpan.Zero)
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("campaigns/{campaignId:guid}/cost-summary")]
    public async Task<ActionResult<AiPlatformCampaignCostSummaryResponse>> GetCampaignCostSummary(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var summary = await _aiCostControlService.GetSummaryAsync(campaignId, null, cancellationToken);
        return Ok(new AiPlatformCampaignCostSummaryResponse
        {
            CampaignId = summary.CampaignId,
            CampaignBudgetZar = summary.CampaignBudgetZar,
            MaxAllowedCostZar = summary.MaxAllowedCostZar,
            CommittedCostZar = summary.CommittedCostZar,
            RemainingBudgetZar = summary.RemainingBudgetZar,
            UtilizationPercent = summary.UtilizationPercent
        });
    }

    [HttpGet("observability")]
    public ActionResult<object> GetObservabilityLinks()
    {
        return Ok(new
        {
            dashboardUrl = _options.DashboardUrl,
            tracesUrl = _options.TracesUrl
        });
    }
}

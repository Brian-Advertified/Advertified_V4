using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/assets")]
public sealed class AiAssetGenerationController : ControllerBase
{
    private readonly IVoiceAssetGenerationService _voiceAssetGenerationService;
    private readonly IImageAssetGenerationService _imageAssetGenerationService;
    private readonly IVideoAssetGenerationService _videoAssetGenerationService;
    private readonly IAssetJobService _assetJobService;
    private readonly IAiCostEstimator _aiCostEstimator;
    private readonly IAiCostControlService _aiCostControlService;

    public AiAssetGenerationController(
        IVoiceAssetGenerationService voiceAssetGenerationService,
        IImageAssetGenerationService imageAssetGenerationService,
        IVideoAssetGenerationService videoAssetGenerationService,
        IAssetJobService assetJobService,
        IAiCostEstimator aiCostEstimator,
        IAiCostControlService aiCostControlService)
    {
        _voiceAssetGenerationService = voiceAssetGenerationService;
        _imageAssetGenerationService = imageAssetGenerationService;
        _videoAssetGenerationService = videoAssetGenerationService;
        _assetJobService = assetJobService;
        _aiCostEstimator = aiCostEstimator;
        _aiCostControlService = aiCostControlService;
    }

    [HttpPost("voice")]
    public async Task<ActionResult<AssetJobResponse>> QueueVoice(
        [FromBody] QueueVoiceAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CampaignId == Guid.Empty || request.CreativeId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId and creativeId are required.");
        }

        var decision = await _aiCostControlService.GuardAsync(new AiCostGuardRequest(
            CampaignId: request.CampaignId,
            Operation: "asset_generation_voice",
            Provider: "ElevenLabs",
            EstimatedCostZar: _aiCostEstimator.EstimateAssetCost("voice"),
            CreativeId: request.CreativeId), cancellationToken);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }

        try
        {
            var queued = await _voiceAssetGenerationService.QueueAsync(new VoiceAssetRequest(
                request.CampaignId,
                request.CreativeId,
                request.Script,
                request.VoiceType,
                request.Language), cancellationToken);
            if (decision.UsageLogId.HasValue)
            {
                await _aiCostControlService.CompleteAsync(decision.UsageLogId.Value, _aiCostEstimator.EstimateAssetCost("voice"), "Voice asset queued.", cancellationToken);
            }

            return Accepted(MapQueued(queued));
        }
        catch (Exception)
        {
            if (decision.UsageLogId.HasValue)
            {
                await _aiCostControlService.FailAsync(decision.UsageLogId.Value, "Voice queueing failed.", cancellationToken);
            }

            throw;
        }
    }

    [HttpPost("image")]
    public async Task<ActionResult<AssetJobResponse>> QueueImage(
        [FromBody] QueueImageAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CampaignId == Guid.Empty || request.CreativeId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId and creativeId are required.");
        }

        var decision = await _aiCostControlService.GuardAsync(new AiCostGuardRequest(
            CampaignId: request.CampaignId,
            Operation: "asset_generation_image",
            Provider: "ImageApi",
            EstimatedCostZar: _aiCostEstimator.EstimateAssetCost("image", Math.Max(1, request.Variations)),
            CreativeId: request.CreativeId), cancellationToken);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }

        try
        {
            var queued = await _imageAssetGenerationService.QueueAsync(new ImageAssetRequest(
                request.CampaignId,
                request.CreativeId,
                request.VisualDirection,
                request.Style,
                request.Variations), cancellationToken);
            if (decision.UsageLogId.HasValue)
            {
                await _aiCostControlService.CompleteAsync(
                    decision.UsageLogId.Value,
                    _aiCostEstimator.EstimateAssetCost("image", Math.Max(1, request.Variations)),
                    "Image asset queued.",
                    cancellationToken);
            }

            return Accepted(MapQueued(queued));
        }
        catch (Exception)
        {
            if (decision.UsageLogId.HasValue)
            {
                await _aiCostControlService.FailAsync(decision.UsageLogId.Value, "Image queueing failed.", cancellationToken);
            }

            throw;
        }
    }

    [HttpPost("video")]
    public async Task<ActionResult<AssetJobResponse>> QueueVideo(
        [FromBody] QueueVideoAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CampaignId == Guid.Empty || request.CreativeId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId and creativeId are required.");
        }

        var summary = await _aiCostControlService.GetSummaryAsync(request.CampaignId, null, cancellationToken);
        if (!_aiCostEstimator.AllowVideoGeneration(summary.CampaignBudgetZar))
        {
            throw new InvalidOperationException("Video generation is disabled for this budget tier.");
        }

        var decision = await _aiCostControlService.GuardAsync(new AiCostGuardRequest(
            CampaignId: request.CampaignId,
            Operation: "asset_generation_video",
            Provider: "Runway",
            EstimatedCostZar: _aiCostEstimator.EstimateAssetCost("video"),
            CampaignBudgetZar: summary.CampaignBudgetZar,
            CreativeId: request.CreativeId), cancellationToken);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }

        try
        {
            var queued = await _videoAssetGenerationService.QueueAsync(new VideoAssetRequest(
                request.CampaignId,
                request.CreativeId,
                request.SceneBreakdownJson,
                request.Script,
                request.Language,
                request.AspectRatio,
                request.DurationSeconds), cancellationToken);
            if (decision.UsageLogId.HasValue)
            {
                await _aiCostControlService.CompleteAsync(decision.UsageLogId.Value, _aiCostEstimator.EstimateAssetCost("video"), "Video asset queued.", cancellationToken);
            }

            return Accepted(MapQueued(queued));
        }
        catch (Exception)
        {
            if (decision.UsageLogId.HasValue)
            {
                await _aiCostControlService.FailAsync(decision.UsageLogId.Value, "Video queueing failed.", cancellationToken);
            }

            throw;
        }
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<AssetJobResponse>> GetJobStatus(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var status = await _assetJobService.GetStatusAsync(jobId, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        return Ok(new AssetJobResponse
        {
            JobId = status.JobId,
            CampaignId = status.CampaignId,
            CreativeId = status.CreativeId,
            AssetKind = status.AssetKind,
            Status = status.Status,
            AssetUrl = status.AssetUrl,
            AssetType = status.AssetType,
            Error = status.Error,
            RetryAttemptCount = status.RetryAttemptCount,
            LastFailure = status.LastFailure,
            UpdatedAt = status.UpdatedAt,
            CompletedAt = status.CompletedAt
        });
    }

    private static AssetJobResponse MapQueued(AssetJobQueuedResult queued)
    {
        return new AssetJobResponse
        {
            JobId = queued.JobId,
            CampaignId = queued.CampaignId,
            CreativeId = queued.CreativeId,
            AssetKind = queued.AssetKind,
            Status = queued.Status,
            RetryAttemptCount = 0,
            UpdatedAt = queued.QueuedAt
        };
    }
}

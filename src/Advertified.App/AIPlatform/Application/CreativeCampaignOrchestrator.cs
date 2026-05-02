using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;

namespace Advertified.App.AIPlatform.Application;

public sealed class CreativeCampaignOrchestrator : ICreativeCampaignOrchestrator
{
    private readonly IMediaPlanningIntegrationService _mediaPlanningIntegrationService;
    private readonly ICreativeGenerationEngine _creativeGenerationEngine;
    private readonly ICreativeQaService _creativeQaService;
    private readonly IAssetGenerationPipeline _assetGenerationPipeline;
    private readonly ICreativeJobQueue _creativeJobQueue;
    private readonly IAiIdempotencyService _aiIdempotencyService;
    private readonly IAiCostEstimator _aiCostEstimator;
    private readonly IAiCostControlService _aiCostControlService;
    private readonly AppDbContext _db;

    public CreativeCampaignOrchestrator(
        IMediaPlanningIntegrationService mediaPlanningIntegrationService,
        ICreativeGenerationEngine creativeGenerationEngine,
        ICreativeQaService creativeQaService,
        IAssetGenerationPipeline assetGenerationPipeline,
        ICreativeJobQueue creativeJobQueue,
        IAiIdempotencyService aiIdempotencyService,
        IAiCostEstimator aiCostEstimator,
        IAiCostControlService aiCostControlService,
        AppDbContext db)
    {
        _mediaPlanningIntegrationService = mediaPlanningIntegrationService;
        _creativeGenerationEngine = creativeGenerationEngine;
        _creativeQaService = creativeQaService;
        _assetGenerationPipeline = assetGenerationPipeline;
        _creativeJobQueue = creativeJobQueue;
        _aiIdempotencyService = aiIdempotencyService;
        _aiCostEstimator = aiCostEstimator;
        _aiCostControlService = aiCostControlService;
        _db = db;
    }

    public async Task<GenerateCampaignCreativesResult> GenerateAsync(
        GenerateCampaignCreativesCommand command,
        CancellationToken cancellationToken)
    {
        var context = await _mediaPlanningIntegrationService.BuildContextAsync(command.CampaignId, cancellationToken);
        var maxVariantsPerChannel = _aiCostEstimator.ResolveVariantCount(context.Budget);

        var brief = BuildBrief(context, command.PromptOverride, maxVariantsPerChannel);
        var estimatedCreativeVariants = Math.Max(1, brief.Channels.Count) * Math.Max(1, brief.Languages.Count) * Math.Max(1, brief.MaxVariantsPerChannel);
        var estimatedCreativeBatchCost = _aiCostEstimator.EstimateTextGenerationCost(estimatedCreativeVariants)
            + _aiCostEstimator.EstimateQaCost(estimatedCreativeVariants);
        var creativeCostDecision = await _aiCostControlService.GuardAsync(new AiCostGuardRequest(
            CampaignId: command.CampaignId,
            Operation: "creative_batch_generation",
            Provider: "OpenAI",
            EstimatedCostZar: estimatedCreativeBatchCost,
            CampaignBudgetZar: context.Budget,
            Details: $"EstimatedVariants={estimatedCreativeVariants}"), cancellationToken);
        if (!creativeCostDecision.Allowed)
        {
            throw new InvalidOperationException(
                $"AI cost cap reached. Current {creativeCostDecision.CurrentCommittedCostZar:0.00} / Max {creativeCostDecision.MaxAllowedCostZar:0.00} ZAR.");
        }

        var creativeReservationId = creativeCostDecision.UsageLogId;
        var approvedAssetReservations = new Dictionary<Guid, Guid>();
        IReadOnlyList<CreativeVariant> creatives;
        IReadOnlyList<CreativeQualityScore> scores;
        IReadOnlyList<AssetGenerationResult> assets;
        try
        {
            creatives = await _creativeGenerationEngine.GenerateAsync(brief, cancellationToken);
            scores = await _creativeQaService.ScoreAsync(brief, creatives, cancellationToken);
            if (creativeReservationId.HasValue)
            {
                var actualCreativeBatchCost = _aiCostEstimator.EstimateTextGenerationCost(creatives.Count)
                    + _aiCostEstimator.EstimateQaCost(scores.Count);
                await _aiCostControlService.CompleteAsync(
                    creativeReservationId.Value,
                    actualCreativeBatchCost,
                    $"Generated={creatives.Count};Scored={scores.Count}",
                    cancellationToken);
            }

            var assetRequests = creatives.Select(item => new AssetGenerationRequest(
                item.CampaignId,
                item.CreativeId,
                item.Channel,
                item.PayloadJson,
                command.VoicePackId)).ToArray();
            var approvedAssetRequests = new List<AssetGenerationRequest>();
            foreach (var request in assetRequests)
            {
                var (assetKind, provider) = ResolveAssetKindAndProvider(request.Channel);
                if (assetKind == "video" && !_aiCostEstimator.AllowVideoGeneration(context.Budget))
                {
                    continue;
                }

                var estimatedAssetCost = _aiCostEstimator.EstimateAssetCost(assetKind, 1);
                var decision = await _aiCostControlService.GuardAsync(new AiCostGuardRequest(
                    CampaignId: request.CampaignId,
                    Operation: $"asset_generation_{assetKind}",
                    Provider: provider,
                    EstimatedCostZar: estimatedAssetCost,
                    CampaignBudgetZar: context.Budget,
                    CreativeId: request.CreativeId,
                    Details: $"Channel={request.Channel}"), cancellationToken);
                if (!decision.Allowed)
                {
                    continue;
                }

                approvedAssetRequests.Add(request);
                if (decision.UsageLogId.HasValue)
                {
                    approvedAssetReservations[request.CreativeId] = decision.UsageLogId.Value;
                }
            }

            assets = await _assetGenerationPipeline.GenerateAssetsAsync(approvedAssetRequests, cancellationToken);
            foreach (var asset in assets)
            {
                if (!approvedAssetReservations.TryGetValue(asset.CreativeId, out var usageLogId))
                {
                    continue;
                }

                var (assetKind, _) = ResolveAssetKindAndProvider(asset.Channel);
                var actualAssetCost = _aiCostEstimator.EstimateAssetCost(assetKind, 1);
                await _aiCostControlService.CompleteAsync(
                    usageLogId,
                    actualAssetCost,
                    $"AssetType={asset.AssetType};Channel={asset.Channel}",
                    cancellationToken);
            }
        }
        catch (Exception)
        {
            if (creativeReservationId.HasValue)
            {
                await _aiCostControlService.FailAsync(creativeReservationId.Value, "Generation pipeline failed.", cancellationToken);
            }

            foreach (var reservation in approvedAssetReservations.Values.Distinct())
            {
                await _aiCostControlService.FailAsync(reservation, "Asset pipeline failed before completion.", cancellationToken);
            }

            throw;
        }

        if (command.PersistOutputs)
        {
            await PersistOutputsAsync(command.CampaignId, creatives, scores, cancellationToken);
        }

        return new GenerateCampaignCreativesResult(
            JobId: Guid.NewGuid(),
            CampaignId: command.CampaignId,
            Creatives: creatives,
            Scores: scores,
            Assets: assets,
            CompletedAt: DateTimeOffset.UtcNow);
    }

    public async Task<QueueCreativeJobStatus> QueueGenerationAsync(
        GenerateCampaignCreativesCommand command,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = BuildIdempotencyKey(command);
        var existingJobId = await _aiIdempotencyService.GetJobIdAsync("creative-generation", idempotencyKey, cancellationToken);
        if (existingJobId.HasValue)
        {
            var existingStatus = await _creativeJobQueue.GetStatusAsync(existingJobId.Value, cancellationToken);
            if (existingStatus is not null)
            {
                return existingStatus;
            }
        }

        var jobId = Guid.NewGuid();
        var request = new QueueCreativeJobRequest(jobId, command, DateTimeOffset.UtcNow);

        await _creativeJobQueue.EnqueueAsync(request, cancellationToken);
        await _aiIdempotencyService.SaveJobIdAsync("creative-generation", idempotencyKey, jobId, cancellationToken);

        var status = new QueueCreativeJobStatus(
            JobId: jobId,
            CampaignId: command.CampaignId,
            Status: "queued",
            Error: null,
            RetryAttemptCount: 0,
            LastFailure: null,
            UpdatedAt: DateTimeOffset.UtcNow);

        await _creativeJobQueue.SetStatusAsync(status, cancellationToken);
        return status;
    }

    private async Task PersistOutputsAsync(
        Guid campaignId,
        IReadOnlyList<CreativeVariant> creatives,
        IReadOnlyList<CreativeQualityScore> scores,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var scoreMap = scores.ToDictionary(item => item.CreativeId, item => item);

        var creativeRows = creatives.Select(creative => new CampaignCreative
        {
            Id = creative.CreativeId,
            CampaignId = campaignId,
            Channel = creative.Channel.ToString(),
            Language = creative.Language,
            CreativeType = $"{creative.Channel} Generated",
            JsonPayload = creative.PayloadJson,
            Score = scoreMap.GetValueOrDefault(creative.CreativeId)?.OverallScore,
            CreatedAt = creative.CreatedAt.UtcDateTime,
            UpdatedAt = now
        }).ToList();

        if (creativeRows.Count > 0)
        {
            _db.CampaignCreatives.AddRange(creativeRows);
        }

        var scoreRows = scores
            .SelectMany(score => score.Metrics.Select(metric => new CreativeScore
            {
                Id = Guid.NewGuid(),
                CampaignCreativeId = score.CreativeId,
                MetricName = metric.Key,
                MetricValue = metric.Value,
                CreatedAt = now
            }))
            .ToList();

        if (scoreRows.Count > 0)
        {
            _db.CreativeScores.AddRange(scoreRows);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildIdempotencyKey(GenerateCampaignCreativesCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            return command.IdempotencyKey.Trim();
        }

        var normalizedPrompt = string.IsNullOrWhiteSpace(command.PromptOverride)
            ? "default"
            : command.PromptOverride.Trim().ToLowerInvariant();

        var voicePackPart = command.VoicePackId.HasValue ? command.VoicePackId.Value.ToString("D") : "default-voice";
        return $"{command.CampaignId:D}:{normalizedPrompt}:{voicePackPart}";
    }

    private static CreativeBrief BuildBrief(
        MediaPlanningContext context,
        string? promptOverride,
        int maxVariantsPerChannel)
    {
        var keyMessage = string.IsNullOrWhiteSpace(promptOverride)
            ? $"High-impact campaign for {context.BusinessName} in {context.Location} with objective {context.Objective}."
            : promptOverride.Trim();

        return new CreativeBrief(
            CampaignId: context.CampaignId,
            Budget: context.Budget,
            Brand: context.BusinessName,
            Objective: context.Objective,
            Tone: context.Tone,
            KeyMessage: keyMessage,
            CallToAction: "Get started today",
            AudienceInsights: new[]
            {
                $"LSM {context.AudienceLsm}",
                $"Age {context.AudienceAgeRange}",
                context.Location
            },
            Languages: context.Languages,
            Channels: context.Channels,
            PromptVersion: 0,
            MaxVariantsPerChannel: maxVariantsPerChannel);
    }

    private static (string AssetKind, string Provider) ResolveAssetKindAndProvider(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => ("voice", "ElevenLabs"),
            AdvertisingChannel.Tv => ("video", "Runway"),
            AdvertisingChannel.Digital => ("video", "Runway"),
            _ => ("image", "ImageApi")
        };
    }
}

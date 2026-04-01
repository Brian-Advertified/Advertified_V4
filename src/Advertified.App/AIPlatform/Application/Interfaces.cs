using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.AIPlatform.Application;

public interface IMediaPlanningIntegrationService
{
    Task<MediaPlanningContext> BuildContextAsync(Guid campaignId, CancellationToken cancellationToken);
}

public interface IPromptLibraryService
{
    Task<PromptTemplate> GetLatestAsync(string key, CancellationToken cancellationToken);
    Task<PromptTemplateDefinition> GetAsync(
        string key,
        AdvertisingChannel channel,
        string language,
        int? version,
        CancellationToken cancellationToken);
    Task<PromptTemplateDefinition> UpsertAsync(PromptTemplateDefinition template, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PromptTemplateDefinition>> ListAsync(
        AdvertisingChannel? channel,
        string? language,
        bool includeInactive,
        CancellationToken cancellationToken);
    Task<PromptRenderResult> RenderAsync(PromptRenderRequest request, CancellationToken cancellationToken);
}

public interface IPromptTemplateRepository
{
    Task<PromptTemplateDefinition?> GetAsync(
        string key,
        AdvertisingChannel channel,
        string language,
        int? version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PromptTemplateDefinition>> ListAsync(
        AdvertisingChannel? channel,
        string? language,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<PromptTemplateDefinition> UpsertAsync(PromptTemplateDefinition template, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICreativeGenerationEngine
{
    Task<IReadOnlyList<CreativeVariant>> GenerateAsync(
        CreativeBrief brief,
        CancellationToken cancellationToken);
}

public interface IPromptInputBuilder
{
    IReadOnlyDictionary<string, string> BuildVariables(
        CreativeBrief brief,
        AdvertisingChannel channel,
        string language,
        string templateKey);
}

public interface ICreativeQaService
{
    Task<IReadOnlyList<CreativeQualityScore>> ScoreAsync(
        CreativeBrief brief,
        IReadOnlyList<CreativeVariant> creatives,
        CancellationToken cancellationToken);
}

public interface ICreativeValidationService
{
    CreativeValidationResult Validate(CreativeVariant creative);
}

public interface ICreativeScoringService
{
    Task<CreativeScoreResult> ScoreAsync(CreativeBrief brief, CreativeVariant creative, CancellationToken cancellationToken);
}

public interface ICreativeRiskService
{
    CreativeRiskResult Analyze(CreativeVariant creative);
}

public interface ICreativeImprovementService
{
    Task<CreativeImprovementResult?> ImproveAsync(
        CreativeBrief brief,
        CreativeVariant creative,
        IReadOnlyList<string> issues,
        IReadOnlyList<string> suggestions,
        CancellationToken cancellationToken);
}

public interface ICreativeDecisionService
{
    string Decide(decimal finalScore, string riskLevel);
}

public interface ICreativeQaResultRepository
{
    Task SaveAsync(IReadOnlyList<CreativeQaResult> results, CancellationToken cancellationToken);
    Task<IReadOnlyList<CreativeQaResult>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken);
}

public interface IAssetGenerationPipeline
{
    Task<IReadOnlyList<AssetGenerationResult>> GenerateAssetsAsync(
        IReadOnlyList<AssetGenerationRequest> requests,
        CancellationToken cancellationToken);
}

public interface IVoiceAssetGenerationService
{
    Task<AssetJobQueuedResult> QueueAsync(VoiceAssetRequest request, CancellationToken cancellationToken);
}

public interface IImageAssetGenerationService
{
    Task<AssetJobQueuedResult> QueueAsync(ImageAssetRequest request, CancellationToken cancellationToken);
}

public interface IVideoAssetGenerationService
{
    Task<AssetJobQueuedResult> QueueAsync(VideoAssetRequest request, CancellationToken cancellationToken);
}

public interface IAssetJobService
{
    Task<AssetJobStatusResult?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken);
}

public interface IAssetJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
    IAsyncEnumerable<AssetJobEnvelope> DequeueAllAsync(CancellationToken cancellationToken);
}

public interface IAssetJobRepository
{
    Task<AssetJobStatusResult> CreateAsync(
        Guid campaignId,
        Guid creativeId,
        string assetKind,
        string provider,
        string requestJson,
        CancellationToken cancellationToken);

    Task<AssetJobStatusResult?> GetAsync(Guid jobId, CancellationToken cancellationToken);
    Task MarkRunningAsync(Guid jobId, int? attemptCount, CancellationToken cancellationToken);
    Task MarkRetryingAsync(Guid jobId, string error, int attemptCount, CancellationToken cancellationToken);
    Task MarkCompletedAsync(Guid jobId, string assetUrl, string assetType, string resultJson, int? attemptCount, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid jobId, string error, int attemptCount, CancellationToken cancellationToken);
    Task<AssetJobPayload?> GetPayloadAsync(Guid jobId, CancellationToken cancellationToken);
}

public sealed record AssetJobPayload(
    Guid JobId,
    Guid CampaignId,
    Guid CreativeId,
    string AssetKind,
    string Provider,
    string RequestJson);

public interface ICreativeFeedbackRegenerationService
{
    Task<GenerateCampaignCreativesResult> RegenerateAsync(
        RegenerationFeedback feedback,
        CancellationToken cancellationToken);
}

public interface ICreativeJobQueue
{
    ValueTask EnqueueAsync(QueueCreativeJobRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<QueueCreativeJobEnvelope> DequeueAllAsync(CancellationToken cancellationToken);
    Task SetStatusAsync(QueueCreativeJobStatus status, CancellationToken cancellationToken);
    Task<QueueCreativeJobStatus?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken);
    Task MoveToDeadLetterAsync(Guid jobId, Guid campaignId, string reason, CancellationToken cancellationToken);
}

public interface IAiIdempotencyService
{
    Task<Guid?> GetJobIdAsync(string scope, string key, CancellationToken cancellationToken);
    Task SaveJobIdAsync(string scope, string key, Guid jobId, CancellationToken cancellationToken);
}

public interface ICreativeCampaignOrchestrator
{
    Task<GenerateCampaignCreativesResult> GenerateAsync(
        GenerateCampaignCreativesCommand command,
        CancellationToken cancellationToken);

    Task<QueueCreativeJobStatus> QueueGenerationAsync(
        GenerateCampaignCreativesCommand command,
        CancellationToken cancellationToken);
}

public interface IAiCostEstimator
{
    decimal CalculateMaxAiCost(decimal campaignBudget);
    int ResolveVariantCount(decimal campaignBudget);
    bool AllowVideoGeneration(decimal campaignBudget);
    decimal EstimateTextGenerationCost(int variantCount);
    decimal EstimateQaCost(int variantCount);
    decimal EstimateAssetCost(string assetKind, int units = 1);
}

public interface IAiCostControlService
{
    Task<AiCostGuardDecision> GuardAsync(AiCostGuardRequest request, CancellationToken cancellationToken);
    Task CompleteAsync(Guid usageLogId, decimal? actualCostZar, string? details, CancellationToken cancellationToken);
    Task FailAsync(Guid usageLogId, string? details, CancellationToken cancellationToken);
    Task<AiCampaignCostSummary> GetSummaryAsync(Guid campaignId, decimal? campaignBudgetZar, CancellationToken cancellationToken);
}

public interface IAiProviderStrategy
{
    string ProviderName { get; }
    bool CanHandle(AdvertisingChannel channel, string operation);
    Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken);
}

public interface IAiProviderStrategyFactory
{
    IAiProviderStrategy GetRequired(AdvertisingChannel channel, string operation);
}

public interface IMultiAiProviderOrchestrator
{
    Task<string> ExecuteAsync(
        AdvertisingChannel channel,
        string operation,
        string inputJson,
        CancellationToken cancellationToken);
}

public interface IVoicePackPolicyService
{
    Task<VoicePackPolicyDecision> EvaluateAsync(
        Guid campaignId,
        Guid? voicePackId,
        VoicePackPolicyInput input,
        CancellationToken cancellationToken);

    Task<VoicePackRecommendationResult?> RecommendAsync(
        Guid campaignId,
        string provider,
        string? audience,
        string? objective,
        decimal? packageBudget,
        string? campaignTier,
        CancellationToken cancellationToken);
}

public interface IVoiceTemplateSelectionService
{
    Task<IReadOnlyList<VoiceTemplateSelectionItem>> ListAsync(CancellationToken cancellationToken);
    Task<VoiceTemplateSelectionResult> SelectAsync(VoiceTemplateSelectionInput input, CancellationToken cancellationToken);
}

using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.AIPlatform.Application;

public sealed record GenerateCampaignCreativesCommand(
    Guid CampaignId,
    string? PromptOverride,
    bool PersistOutputs,
    string? IdempotencyKey = null);

public sealed record GenerateCampaignCreativesResult(
    Guid JobId,
    Guid CampaignId,
    IReadOnlyList<CreativeVariant> Creatives,
    IReadOnlyList<CreativeQualityScore> Scores,
    IReadOnlyList<AssetGenerationResult> Assets,
    DateTimeOffset CompletedAt);

public sealed record QueueCreativeJobRequest(
    Guid JobId,
    GenerateCampaignCreativesCommand Command,
    DateTimeOffset EnqueuedAt);

public sealed record QueueCreativeJobStatus(
    Guid JobId,
    Guid CampaignId,
    string Status,
    string? Error,
    int RetryAttemptCount,
    string? LastFailure,
    DateTimeOffset UpdatedAt);

public sealed record QueueCreativeJobEnvelope(
    QueueCreativeJobRequest Request,
    int DeliveryCount,
    Func<CancellationToken, Task> CompleteAsync,
    Func<CancellationToken, Task> AbandonAsync,
    Func<string, string, CancellationToken, Task> DeadLetterAsync);

public sealed record AssetJobEnvelope(
    Guid JobId,
    int DeliveryCount,
    Func<CancellationToken, Task> CompleteAsync,
    Func<CancellationToken, Task> AbandonAsync,
    Func<string, string, CancellationToken, Task> DeadLetterAsync);

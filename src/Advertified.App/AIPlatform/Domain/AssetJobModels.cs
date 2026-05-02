namespace Advertified.App.AIPlatform.Domain;

public sealed record VoiceAssetRequest(
    Guid CampaignId,
    Guid CreativeId,
    string Script,
    string VoiceType,
    string Language,
    Guid? VoicePackId = null);

public sealed record ImageAssetRequest(
    Guid CampaignId,
    Guid CreativeId,
    string VisualDirection,
    string Style,
    int Variations);

public sealed record VideoAssetRequest(
    Guid CampaignId,
    Guid CreativeId,
    string SceneBreakdownJson,
    string Script,
    string Language,
    string AspectRatio,
    int DurationSeconds);

public sealed record AssetJobQueuedResult(
    Guid JobId,
    Guid CampaignId,
    Guid CreativeId,
    string AssetKind,
    string Status,
    DateTimeOffset QueuedAt);

public sealed record AssetJobStatusResult(
    Guid JobId,
    Guid CampaignId,
    Guid CreativeId,
    string AssetKind,
    string Status,
    string? AssetUrl,
    string? AssetType,
    string? Error,
    int RetryAttemptCount,
    string? LastFailure,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

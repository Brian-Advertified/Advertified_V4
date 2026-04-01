namespace Advertified.App.AIPlatform.Api;

public sealed class QueueVoiceAssetRequest
{
    public Guid CampaignId { get; set; }
    public Guid CreativeId { get; set; }
    public string Script { get; set; } = string.Empty;
    public string VoiceType { get; set; } = "Standard";
    public string Language { get; set; } = "English";
}

public sealed class QueueImageAssetRequest
{
    public Guid CampaignId { get; set; }
    public Guid CreativeId { get; set; }
    public string VisualDirection { get; set; } = string.Empty;
    public string Style { get; set; } = "Bold";
    public int Variations { get; set; } = 1;
}

public sealed class QueueVideoAssetRequest
{
    public Guid CampaignId { get; set; }
    public Guid CreativeId { get; set; }
    public string SceneBreakdownJson { get; set; } = "{}";
    public string Script { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public int DurationSeconds { get; set; } = 30;
}

public sealed class AssetJobResponse
{
    public Guid JobId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid CreativeId { get; set; }
    public string AssetKind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssetUrl { get; set; }
    public string? AssetType { get; set; }
    public string? Error { get; set; }
    public int RetryAttemptCount { get; set; }
    public string? LastFailure { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

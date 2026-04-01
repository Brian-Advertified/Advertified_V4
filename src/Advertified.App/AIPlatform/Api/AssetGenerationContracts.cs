namespace Advertified.App.AIPlatform.Api;

public sealed class QueueVoiceAssetRequest
{
    public Guid CampaignId { get; set; }
    public Guid CreativeId { get; set; }
    public string Script { get; set; } = string.Empty;
    public string VoiceType { get; set; } = "Standard";
    public Guid? VoicePackId { get; set; }
    public string Language { get; set; } = "English";
    public string? Audience { get; set; }
    public string? Objective { get; set; }
    public decimal? PackageBudget { get; set; }
    public string? CampaignTier { get; set; }
    public bool AllowTierUpsell { get; set; }
    public bool GenerateSaLanguageVariants { get; set; }
    public string[]? RequestedLanguages { get; set; }
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
    public string AspectRatio { get; set; } = "16:9";
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
    public Guid? AppliedVoicePackId { get; set; }
    public string? AppliedLanguage { get; set; }
    public bool? UpsellRequired { get; set; }
    public string? UpsellMessage { get; set; }
    public VoiceQaResponse? VoiceQa { get; set; }
    public Guid[]? VariantJobIds { get; set; }
}

public sealed class VoiceQaResponse
{
    public decimal Authenticity { get; set; }
    public decimal Clarity { get; set; }
    public decimal ConversionPotential { get; set; }
    public string[] Notes { get; set; } = Array.Empty<string>();
    public bool ModerationPassed { get; set; }
    public string[] ModerationFlags { get; set; } = Array.Empty<string>();
    public string[] ModerationSuggestions { get; set; } = Array.Empty<string>();
}

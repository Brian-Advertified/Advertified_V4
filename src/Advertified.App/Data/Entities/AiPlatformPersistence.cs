namespace Advertified.App.Data.Entities;

public sealed class AiPromptTemplate
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Channel { get; set; } = "Digital";
    public string Language { get; set; } = "English";
    public int Version { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string TemplatePrompt { get; set; } = string.Empty;
    public string OutputSchema { get; set; } = string.Empty;
    public string VariablesJson { get; set; } = "[]";
    public string VersionLabel { get; set; } = "v1";
    public decimal? PerformanceScore { get; set; }
    public int UsageCount { get; set; }
    public string? BaseSystemPromptKey { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AiCreativeJobStatus
{
    public Guid JobId { get; set; }
    public Guid CampaignId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int RetryAttemptCount { get; set; }
    public string? LastFailure { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiCreativeQaResult
{
    public Guid Id { get; set; }
    public Guid CreativeId { get; set; }
    public Guid CampaignId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public decimal Clarity { get; set; }
    public decimal Attention { get; set; }
    public decimal EmotionalImpact { get; set; }
    public decimal CtaStrength { get; set; }
    public decimal BrandFit { get; set; }
    public decimal ChannelFit { get; set; }
    public decimal FinalScore { get; set; }
    public string Status { get; set; } = "NeedsImprovement";
    public string RiskLevel { get; set; } = "Low";
    public string IssuesJson { get; set; } = "[]";
    public string SuggestionsJson { get; set; } = "[]";
    public string RiskFlagsJson { get; set; } = "[]";
    public string? ImprovedPayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AiAssetJob
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid CreativeId { get; set; }
    public string AssetKind { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public string RequestJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? AssetUrl { get; set; }
    public string? AssetType { get; set; }
    public string? Error { get; set; }
    public int RetryAttemptCount { get; set; }
    public string? LastFailure { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class AiCreativeJobDeadLetter
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid CampaignId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class AiIdempotencyRecord
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public Guid JobId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AiUsageLog
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? CreativeId { get; set; }
    public Guid? JobId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public decimal EstimatedCostZar { get; set; }
    public decimal? ActualCostZar { get; set; }
    public string Status { get; set; } = "reserved";
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiVoiceProfile
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "ElevenLabs";
    public string Label { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string? Language { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiVoicePack
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "ElevenLabs";
    public string Name { get; set; } = string.Empty;
    public string? Accent { get; set; }
    public string? Language { get; set; }
    public string? Tone { get; set; }
    public string? Persona { get; set; }
    public string UseCasesJson { get; set; } = "[]";
    public string VoiceId { get; set; } = string.Empty;
    public string? SampleAudioUrl { get; set; }
    public string PromptTemplate { get; set; } = string.Empty;
    public string PricingTier { get; set; } = "standard";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

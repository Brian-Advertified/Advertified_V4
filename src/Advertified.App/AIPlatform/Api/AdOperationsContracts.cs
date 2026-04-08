namespace Advertified.App.AIPlatform.Api;

public sealed class CreateAdVariantRequest
{
    public Guid CampaignId { get; set; }
    public Guid? CampaignCreativeId { get; set; }
    public string Platform { get; set; } = "Meta";
    public string Channel { get; set; } = "Digital";
    public string Language { get; set; } = "English";
    public int? TemplateId { get; set; }
    public Guid? VoicePackId { get; set; }
    public string? VoicePackName { get; set; }
    public string Script { get; set; } = string.Empty;
    public string? AudioAssetUrl { get; set; }
}

public sealed class AdVariantResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? CampaignCreativeId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int? TemplateId { get; set; }
    public Guid? VoicePackId { get; set; }
    public string? VoicePackName { get; set; }
    public string Script { get; set; } = string.Empty;
    public string? AudioAssetUrl { get; set; }
    public string? PlatformAdId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class PublishAdVariantResponse
{
    public Guid VariantId { get; set; }
    public Guid CampaignId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string PlatformAdId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
}

public sealed class TrackConversionRequest
{
    public int Conversions { get; set; } = 1;
}

public sealed class CampaignAdMetricsSummaryResponse
{
    public Guid CampaignId { get; set; }
    public int VariantCount { get; set; }
    public int PublishedVariantCount { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int Conversions { get; set; }
    public decimal CostZar { get; set; }
    public decimal Ctr { get; set; }
    public decimal ConversionRate { get; set; }
    public Guid? TopVariantId { get; set; }
    public decimal? TopVariantConversionRate { get; set; }
    public DateTimeOffset? LastRecordedAt { get; set; }
}

public sealed class SyncCampaignMetricsResponse
{
    public Guid CampaignId { get; set; }
    public int SyncedVariantCount { get; set; }
    public CampaignAdMetricsSummaryResponse Summary { get; set; } = new();
}

public sealed class OptimizeCampaignResponse
{
    public Guid CampaignId { get; set; }
    public Guid? PromotedVariantId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OptimizedAt { get; set; }
}

public sealed class UpsertCampaignAdPlatformConnectionRequest
{
    public string Provider { get; set; } = "Meta";
    public string ExternalAccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? ExternalCampaignId { get; set; }
    public bool IsPrimary { get; set; }
    public string Status { get; set; } = "active";
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }
}

public sealed class CampaignAdPlatformConnectionResponse
{
    public Guid LinkId { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid CampaignId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalAccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? ExternalCampaignId { get; set; }
    public bool IsPrimary { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

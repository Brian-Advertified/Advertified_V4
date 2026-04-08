namespace Advertified.App.AIPlatform.Domain;

public sealed record CreateAdVariantCommand(
    Guid CampaignId,
    Guid? CampaignCreativeId,
    string Platform,
    string Channel,
    string Language,
    int? TemplateId,
    Guid? VoicePackId,
    string? VoicePackName,
    string Script,
    string? AudioAssetUrl);

public sealed record AdVariantSummary(
    Guid Id,
    Guid CampaignId,
    Guid? CampaignCreativeId,
    string Platform,
    string Channel,
    string Language,
    int? TemplateId,
    Guid? VoicePackId,
    string? VoicePackName,
    string Script,
    string? AudioAssetUrl,
    string? PlatformAdId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    int Impressions = 0,
    int Clicks = 0,
    int Conversions = 0,
    decimal CostZar = 0m,
    decimal Ctr = 0m,
    decimal ConversionRate = 0m,
    decimal? CplZar = null,
    decimal? Roas = null);

public sealed record ExternalAdMetrics(
    int Impressions,
    int Clicks,
    int Conversions,
    decimal CostZar);

public sealed record CampaignAdMetricsSummary(
    Guid CampaignId,
    int VariantCount,
    int PublishedVariantCount,
    int Impressions,
    int Clicks,
    int Conversions,
    decimal CostZar,
    decimal Ctr,
    decimal ConversionRate,
    decimal? CplZar,
    decimal? Roas,
    Guid? TopVariantId,
    decimal? TopVariantConversionRate,
    DateTimeOffset? LastRecordedAt);

public sealed record PublishAdVariantResult(
    Guid VariantId,
    Guid CampaignId,
    string Platform,
    string PlatformAdId,
    string Status,
    DateTimeOffset PublishedAt);

public sealed record SyncCampaignMetricsResult(
    Guid CampaignId,
    int SyncedVariantCount,
    CampaignAdMetricsSummary Summary);

public sealed record OptimizeCampaignResult(
    Guid CampaignId,
    Guid? PromotedVariantId,
    string Message,
    DateTimeOffset OptimizedAt);

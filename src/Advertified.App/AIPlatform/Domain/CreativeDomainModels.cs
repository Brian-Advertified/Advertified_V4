namespace Advertified.App.AIPlatform.Domain;

public sealed record MediaPlanningContext(
    Guid CampaignId,
    string BusinessName,
    string Industry,
    string Location,
    string Objective,
    decimal Budget,
    string Tone,
    string AudienceLsm,
    string AudienceAgeRange,
    IReadOnlyList<string> Languages,
    IReadOnlyList<AdvertisingChannel> Channels);

public sealed record PromptTemplate(
    string Key,
    int Version,
    string SystemPrompt,
    string TemplatePrompt,
    string OutputSchema);

public sealed record CreativeBrief(
    Guid CampaignId,
    decimal Budget,
    string Brand,
    string Objective,
    string Tone,
    string KeyMessage,
    string CallToAction,
    IReadOnlyList<string> AudienceInsights,
    IReadOnlyList<string> Languages,
    IReadOnlyList<AdvertisingChannel> Channels,
    int PromptVersion,
    int MaxVariantsPerChannel);

public sealed record CreativeVariant(
    Guid CreativeId,
    Guid CampaignId,
    AdvertisingChannel Channel,
    string Language,
    string PayloadJson,
    DateTimeOffset CreatedAt);

public sealed record CreativeQualityScore(
    Guid CreativeId,
    AdvertisingChannel Channel,
    IReadOnlyDictionary<string, decimal> Metrics,
    decimal OverallScore,
    string Status = "Approved",
    string RiskLevel = "Low",
    IReadOnlyList<string>? Issues = null,
    IReadOnlyList<string>? Suggestions = null);

public sealed record AssetGenerationRequest(
    Guid CampaignId,
    Guid CreativeId,
    AdvertisingChannel Channel,
    string PayloadJson,
    Guid? VoicePackId = null);

public sealed record AssetGenerationResult(
    Guid CreativeId,
    AdvertisingChannel Channel,
    string AssetType,
    string AssetUrl,
    DateTimeOffset CreatedAt);

public sealed record RegenerationFeedback(
    Guid CreativeId,
    Guid CampaignId,
    string Feedback,
    DateTimeOffset SubmittedAt);

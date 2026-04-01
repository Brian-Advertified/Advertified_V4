namespace Advertified.App.AIPlatform.Domain;

public sealed record CreativeValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record CreativeScoreResult(
    decimal Clarity,
    decimal Attention,
    decimal EmotionalImpact,
    decimal CtaStrength,
    decimal BrandFit,
    decimal ChannelFit,
    decimal FinalScore,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Suggestions);

public sealed record CreativeRiskResult(
    string RiskLevel,
    IReadOnlyList<string> Flags,
    string Action);

public sealed record CreativeImprovementResult(
    string UpdatedAdJson,
    IReadOnlyList<string> Changes);

public sealed record CreativeQaResult(
    Guid CreativeId,
    Guid CampaignId,
    AdvertisingChannel Channel,
    string Language,
    decimal Clarity,
    decimal Attention,
    decimal EmotionalImpact,
    decimal CtaStrength,
    decimal BrandFit,
    decimal ChannelFit,
    decimal FinalScore,
    string Status,
    string RiskLevel,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Suggestions,
    IReadOnlyList<string> RiskFlags,
    string? ImprovedPayloadJson,
    DateTimeOffset CreatedAt);

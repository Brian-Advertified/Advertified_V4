namespace Advertified.App.AIPlatform.Domain;

public sealed record VoicePackPolicyInput(
    string Script,
    string? RequestedLanguage,
    string? Audience,
    string? Objective,
    decimal? PackageBudget,
    string? CampaignTier,
    bool AllowTierUpsell);

public sealed record VoicePackModerationResult(
    bool IsAllowed,
    string[] Flags,
    string[] Suggestions);

public sealed record VoicePackQaScore(
    decimal Authenticity,
    decimal Clarity,
    decimal ConversionPotential,
    string[] Notes);

public sealed record VoicePackPolicyDecision(
    string AppliedLanguage,
    bool UpsellRequired,
    string? UpsellMessage,
    VoicePackModerationResult Moderation,
    VoicePackQaScore QaScore);

public sealed record VoicePackRecommendationResult(
    Guid VoicePackId,
    string Reason,
    decimal MatchScore);

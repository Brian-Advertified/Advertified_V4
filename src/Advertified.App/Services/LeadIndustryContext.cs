using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadIndustryContext
{
    public MasterIndustryMatch? CanonicalIndustry { get; init; }

    public string Code => CanonicalIndustry?.Code ?? Policy.Key ?? string.Empty;

    public string Label => CanonicalIndustry?.Label ?? Policy.Name ?? string.Empty;

    public LeadIndustryPolicyProfile Policy { get; init; } = new();

    public IndustryArchetypeScoringProfile? ArchetypeScoringProfile { get; init; }

    public IndustryAudienceProfile Audience { get; init; } = new();

    public IndustryCampaignProfile Campaign { get; init; } = new();

    public IndustryChannelProfile Channels { get; init; } = new();

    public IndustryCreativeProfile Creative { get; init; } = new();

    public IndustryComplianceProfile Compliance { get; init; } = new();

    public IndustryPlanningProfile Planning { get; init; } = new();

    public IndustryResearchProfile Research { get; init; } = new();
}

public sealed class IndustryAudienceProfile
{
    public string PrimaryPersona { get; init; } = string.Empty;

    public string BuyingJourney { get; init; } = string.Empty;

    public string TrustSensitivity { get; init; } = string.Empty;

    public IReadOnlyList<string> DefaultLanguageBiases { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AudienceHints { get; init; } = Array.Empty<string>();
}

public sealed class IndustryCampaignProfile
{
    public string DefaultObjective { get; init; } = string.Empty;

    public string FunnelShape { get; init; } = string.Empty;

    public IReadOnlyList<string> PrimaryKpis { get; init; } = Array.Empty<string>();

    public string SalesCycle { get; init; } = string.Empty;
}

public sealed class IndustryChannelProfile
{
    public IReadOnlyList<string> PreferredChannels { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, int> BaseBudgetSplit { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public string GeographyBias { get; init; } = string.Empty;
}

public sealed class IndustryCreativeProfile
{
    public string PreferredTone { get; init; } = string.Empty;

    public string MessagingAngle { get; init; } = string.Empty;

    public string RecommendedCta { get; init; } = string.Empty;

    public IReadOnlyList<string> ProofPoints { get; init; } = Array.Empty<string>();
}

public sealed class IndustryComplianceProfile
{
    public IReadOnlyList<string> Guardrails { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RestrictedClaimTypes { get; init; } = Array.Empty<string>();
}

public sealed class IndustryPlanningProfile
{
    public string ArchetypeCode { get; init; } = string.Empty;

    public decimal MetadataTagMatchScore { get; init; }

    public IReadOnlyDictionary<string, decimal> MediaTypeScores { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, decimal> AudienceHintScores { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
}

public sealed class IndustryResearchProfile
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();
}

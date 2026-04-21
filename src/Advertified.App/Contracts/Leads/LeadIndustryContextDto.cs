namespace Advertified.App.Contracts.Leads;

public sealed class LeadIndustryContextDto
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public LeadIndustryPolicyDto Policy { get; init; } = new();

    public LeadIndustryAudienceProfileDto Audience { get; init; } = new();

    public LeadIndustryCampaignProfileDto Campaign { get; init; } = new();

    public LeadIndustryChannelProfileDto Channels { get; init; } = new();

    public LeadIndustryCreativeProfileDto Creative { get; init; } = new();

    public LeadIndustryComplianceProfileDto Compliance { get; init; } = new();

    public LeadIndustryResearchProfileDto Research { get; init; } = new();
}

public sealed class LeadIndustryAudienceProfileDto
{
    public string PrimaryPersona { get; init; } = string.Empty;

    public string BuyingJourney { get; init; } = string.Empty;

    public string TrustSensitivity { get; init; } = string.Empty;

    public IReadOnlyList<string> DefaultLanguageBiases { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AudienceHints { get; init; } = Array.Empty<string>();
}

public sealed class LeadIndustryCampaignProfileDto
{
    public string DefaultObjective { get; init; } = string.Empty;

    public string FunnelShape { get; init; } = string.Empty;

    public IReadOnlyList<string> PrimaryKpis { get; init; } = Array.Empty<string>();

    public string SalesCycle { get; init; } = string.Empty;
}

public sealed class LeadIndustryChannelProfileDto
{
    public IReadOnlyList<string> PreferredChannels { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, int> BaseBudgetSplit { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public string GeographyBias { get; init; } = string.Empty;
}

public sealed class LeadIndustryCreativeProfileDto
{
    public string PreferredTone { get; init; } = string.Empty;

    public string MessagingAngle { get; init; } = string.Empty;

    public string RecommendedCta { get; init; } = string.Empty;

    public IReadOnlyList<string> ProofPoints { get; init; } = Array.Empty<string>();
}

public sealed class LeadIndustryComplianceProfileDto
{
    public IReadOnlyList<string> Guardrails { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RestrictedClaimTypes { get; init; } = Array.Empty<string>();
}

public sealed class LeadIndustryResearchProfileDto
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();
}

namespace Advertified.App.Contracts.Admin;

public class CreateAdminIndustryStrategyProfileRequest
{
    public string IndustryCode { get; set; } = string.Empty;
    public string IndustryLabel { get; set; } = string.Empty;
    public string PrimaryPersona { get; set; } = string.Empty;
    public string BuyingJourney { get; set; } = string.Empty;
    public string TrustSensitivity { get; set; } = string.Empty;
    public IReadOnlyList<string> DefaultLanguageBiases { get; set; } = Array.Empty<string>();
    public string DefaultObjective { get; set; } = string.Empty;
    public string FunnelShape { get; set; } = string.Empty;
    public IReadOnlyList<string> PrimaryKpis { get; set; } = Array.Empty<string>();
    public string SalesCycle { get; set; } = string.Empty;
    public IReadOnlyList<string> PreferredChannels { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, int> BaseBudgetSplit { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public string GeographyBias { get; set; } = string.Empty;
    public string PreferredTone { get; set; } = string.Empty;
    public string MessagingAngle { get; set; } = string.Empty;
    public string RecommendedCta { get; set; } = string.Empty;
    public IReadOnlyList<string> ProofPoints { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Guardrails { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RestrictedClaimTypes { get; set; } = Array.Empty<string>();
    public string ResearchSummary { get; set; } = string.Empty;
    public IReadOnlyList<string> ResearchSources { get; set; } = Array.Empty<string>();
}

public sealed class UpdateAdminIndustryStrategyProfileRequest : CreateAdminIndustryStrategyProfileRequest
{
}

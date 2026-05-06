using Advertified.App.Data.Entities;

namespace Advertified.App.Campaigns;

internal sealed record RecommendationProposalCampaignContext(
    string ClientName,
    string? BusinessName,
    string CampaignName,
    string PackageName,
    decimal SelectedBudget,
    string BudgetLabel,
    string BudgetDisplayText,
    string? CampaignObjective,
    string? SpecialRequirements,
    IReadOnlyList<string> TargetAreas,
    string? TargetAudienceSummary,
    IReadOnlyList<string> TargetLanguages);

internal sealed record RecommendationProposalIntelligenceRequest(
    RecommendationProposalCampaignContext Campaign,
    CampaignRecommendation Recommendation,
    RecommendationOpportunityContextModel? OpportunityContext,
    IReadOnlyList<RecommendationLineDocumentModel> Items,
    int ProposalIndex);

internal sealed record RecommendationProposalIntelligenceResult(
    string Label,
    string Strategy,
    string Summary,
    string Rationale,
    RecommendationProposalNarrativeDocumentModel Narrative);

internal sealed class RecommendationProposalNarrativeDocumentModel
{
    public string? ClientChallenge { get; init; }
    public string? StrategicApproach { get; init; }
    public string? ExpectedOutcome { get; init; }
    public IReadOnlyList<string> ChannelRoles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SuccessMeasures { get; init; } = Array.Empty<string>();

    public bool HasContent =>
        !string.IsNullOrWhiteSpace(ClientChallenge)
        || !string.IsNullOrWhiteSpace(StrategicApproach)
        || !string.IsNullOrWhiteSpace(ExpectedOutcome)
        || ChannelRoles.Count > 0
        || SuccessMeasures.Count > 0;
}

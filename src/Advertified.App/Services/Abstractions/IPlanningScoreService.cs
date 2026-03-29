using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningScoreService
{
    PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request);
    decimal GeoScore(InventoryCandidate candidate, CampaignPlanningRequest request);
    decimal AudienceScore(InventoryCandidate candidate, CampaignPlanningRequest request);
    decimal BudgetScore(InventoryCandidate candidate, CampaignPlanningRequest request);
    decimal MediaPreferenceScore(InventoryCandidate candidate, CampaignPlanningRequest request);
    decimal MixTargetScore(InventoryCandidate candidate, CampaignPlanningRequest request);
}


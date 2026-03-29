using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IRecommendationExplainabilityService
{
    PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request);
    string BuildRationale(List<PlannedItem> basePlan, List<PlannedItem> recommendedPlan, CampaignPlanningRequest request);
    IReadOnlyList<string> GetPreferredMediaFallbackFlags(CampaignPlanningRequest request, List<PlannedItem> recommendedPlan);
}


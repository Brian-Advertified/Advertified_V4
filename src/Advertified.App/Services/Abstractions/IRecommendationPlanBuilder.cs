using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IRecommendationPlanBuilder
{
    List<PlannedItem> BuildPlan(List<InventoryCandidate> candidates, CampaignPlanningRequest request, bool diversify);
    List<PlannedItem> BuildUpsells(List<InventoryCandidate> candidates, List<PlannedItem> recommendedPlan, decimal upsellHeadroom);
}

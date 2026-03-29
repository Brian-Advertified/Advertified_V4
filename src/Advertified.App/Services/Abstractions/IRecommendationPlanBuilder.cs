using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IRecommendationPlanBuilder
{
    List<PlannedItem> BuildPlan(List<InventoryCandidate> candidates, decimal budget, int? maxItems, bool diversify);
    List<PlannedItem> BuildUpsells(List<InventoryCandidate> candidates, List<PlannedItem> recommendedPlan, decimal upsellHeadroom);
}


namespace Advertified.App.Domain.Campaigns;

public sealed class RecommendationResult
{
    public List<PlannedItem> BasePlan { get; set; } = new();
    public List<PlannedItem> RecommendedPlan { get; set; } = new();
    public List<PlannedItem> Upsells { get; set; } = new();
    public decimal BasePlanTotal => BasePlan.Sum(x => x.TotalCost);
    public decimal RecommendedPlanTotal => RecommendedPlan.Sum(x => x.TotalCost);
    public decimal UpsellTotal => Upsells.Sum(x => x.TotalCost);
    public string Rationale { get; set; } = string.Empty;
}

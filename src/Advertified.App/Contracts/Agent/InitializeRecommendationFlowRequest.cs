using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Contracts.Agent;

public sealed class InitializeRecommendationFlowRequest
{
    public string? CampaignName { get; set; }
    public string PlanningMode { get; set; } = "hybrid";
    public bool SubmitBrief { get; set; } = true;
    public SaveCampaignBriefRequest Brief { get; set; } = new();
}

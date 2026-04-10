using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningRequestFactory
{
    CampaignPlanningRequest FromCampaignBrief(
        Campaign campaign,
        CampaignBrief brief,
        GenerateRecommendationRequest? request,
        PackageBandProfile? packageProfile);
}

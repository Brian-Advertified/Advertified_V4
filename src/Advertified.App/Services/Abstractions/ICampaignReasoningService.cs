using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignReasoningService
{
    Task<CampaignReasoningResult?> GenerateAsync(
        CampaignEntity campaign,
        CampaignBriefEntity brief,
        CampaignPlanningRequest planningRequest,
        RecommendationResult recommendationResult,
        CancellationToken cancellationToken);
}

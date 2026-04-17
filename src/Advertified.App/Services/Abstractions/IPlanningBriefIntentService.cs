using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningBriefIntentService
{
    PlanningBriefIntentEvaluation EvaluateCandidate(InventoryCandidate candidate, CampaignPlanningRequest request);
}

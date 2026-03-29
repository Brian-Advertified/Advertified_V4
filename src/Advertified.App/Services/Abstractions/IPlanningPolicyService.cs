using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IPlanningPolicyService
{
    PlanningPolicyOutcome ApplyHigherBandRadioEligibility(List<InventoryCandidate> candidates, CampaignPlanningRequest request);
    decimal GetHigherBandRadioBonus(InventoryCandidate candidate, CampaignPlanningRequest request);
    bool IsNationalCapableRadioCandidate(InventoryCandidate candidate, CampaignPlanningRequest request);
    string GetPricingModel(InventoryCandidate candidate);
    bool IsRepeatableCandidate(InventoryCandidate candidate);
    int? GetTargetShare(string? mediaType, CampaignPlanningRequest request);
    string? BuildRequestedMixLabel(CampaignPlanningRequest request);
}


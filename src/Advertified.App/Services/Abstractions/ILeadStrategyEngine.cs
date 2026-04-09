namespace Advertified.App.Services.Abstractions;

public interface ILeadStrategyEngine
{
    LeadStrategyResult Build(
        LeadBusinessProfile businessProfile,
        LeadIndustryPolicyProfile industryPolicy,
        LeadOpportunityProfile opportunityProfile,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections);
}

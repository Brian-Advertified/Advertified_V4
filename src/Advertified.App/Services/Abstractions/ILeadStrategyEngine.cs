namespace Advertified.App.Services.Abstractions;

public interface ILeadStrategyEngine
{
    LeadStrategyResult Build(
        LeadBusinessProfile businessProfile,
        LeadIndustryPolicyProfile industryPolicy,
        LeadIndustryContext? industryContext,
        LeadOpportunityProfile opportunityProfile,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections);
}

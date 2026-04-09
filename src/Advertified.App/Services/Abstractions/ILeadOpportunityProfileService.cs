using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadOpportunityProfileService
{
    LeadOpportunityProfile Build(
        Lead lead,
        Signal? latestSignal,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections,
        LeadIndustryPolicyProfile industryPolicy);
}

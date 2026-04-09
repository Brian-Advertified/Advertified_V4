using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadBusinessProfileService
{
    LeadBusinessProfile Build(
        Lead lead,
        LeadEnrichmentSnapshot enrichmentSnapshot,
        LeadIndustryPolicyProfile industryPolicy,
        LeadOpportunityProfile opportunityProfile);
}

namespace Advertified.App.Services.Abstractions;

public interface ILeadIndustryPolicyService
{
    LeadIndustryPolicyProfile ResolveForCategory(string? category);
}

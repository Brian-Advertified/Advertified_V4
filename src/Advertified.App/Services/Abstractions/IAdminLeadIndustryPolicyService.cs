using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IAdminLeadIndustryPolicyService
{
    Task CreateAsync(CreateAdminLeadIndustryPolicyRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(string key, UpdateAdminLeadIndustryPolicyRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

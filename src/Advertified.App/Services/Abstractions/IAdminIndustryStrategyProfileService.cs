using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IAdminIndustryStrategyProfileService
{
    Task CreateAsync(CreateAdminIndustryStrategyProfileRequest request, CancellationToken cancellationToken);

    Task UpdateAsync(string industryCode, UpdateAdminIndustryStrategyProfileRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(string industryCode, CancellationToken cancellationToken);
}

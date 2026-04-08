using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IAdminDashboardService
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AdminOutletPageResponse> GetOutletPageAsync(int page, int pageSize, bool issuesOnly, string sortBy, CancellationToken cancellationToken);
}

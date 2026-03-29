using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IAdminDashboardService
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);
}

using Advertified.App.Contracts.Agent;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadOpsCoverageService
{
    Task<LeadOpsCoverageResponse> BuildAsync(UserAccount currentUser, CancellationToken cancellationToken);

    Task<LeadOpsCoverageResponse> BuildSystemSnapshotAsync(CancellationToken cancellationToken);
}

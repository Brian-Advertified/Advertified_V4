using Advertified.App.Contracts.Agent;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadOpsInboxService
{
    Task<LeadOpsInboxResponse> BuildAsync(UserAccount currentUser, CancellationToken cancellationToken);

    Task<LeadOpsInboxResponse> BuildSystemSnapshotAsync(CancellationToken cancellationToken);
}

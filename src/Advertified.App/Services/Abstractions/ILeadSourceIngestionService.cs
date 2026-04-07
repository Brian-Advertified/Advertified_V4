using Advertified.App.Contracts.Leads;

namespace Advertified.App.Services.Abstractions;

public interface ILeadSourceIngestionService
{
    Task<LeadSourceIngestionResult> IngestAsync(
        IReadOnlyList<IngestLeadSourceItemRequest> leads,
        CancellationToken cancellationToken);
}

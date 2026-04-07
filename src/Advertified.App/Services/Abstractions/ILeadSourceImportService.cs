using Advertified.App.Contracts.Leads;

namespace Advertified.App.Services.Abstractions;

public interface ILeadSourceImportService
{
    Task<LeadSourceIngestionResult> ImportCsvAsync(
        string csvText,
        string defaultSource,
        CancellationToken cancellationToken);
}

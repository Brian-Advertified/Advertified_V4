using Advertified.App.Contracts.Leads;

namespace Advertified.App.Services.Abstractions;

public interface IGoogleSheetsLeadIntegrationService
{
    GoogleSheetsLeadIntegrationStatusDto GetStatus();

    Task<GoogleSheetsLeadIntegrationRunDto> ImportConfiguredSourcesAsync(CancellationToken cancellationToken);

    Task<GoogleSheetsLeadIntegrationRunDto> ExportLeadOpsSnapshotAsync(CancellationToken cancellationToken);
}

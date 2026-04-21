namespace Advertified.App.Contracts.Leads;

public sealed class GoogleSheetsLeadIntegrationStatusDto
{
    public bool Enabled { get; init; }

    public bool ImportEnabled { get; init; }

    public bool ExportEnabled { get; init; }

    public int ImportPollIntervalMinutes { get; init; }

    public int ExportPollIntervalMinutes { get; init; }

    public bool ExportWebhookConfigured { get; init; }

    public int ConfiguredSourceCount { get; init; }

    public int ActiveSourceCount { get; init; }

    public IReadOnlyList<GoogleSheetsLeadSourceStatusDto> Sources { get; init; } = Array.Empty<GoogleSheetsLeadSourceStatusDto>();
}

public sealed class GoogleSheetsLeadSourceStatusDto
{
    public string Name { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public string DefaultSource { get; init; } = string.Empty;

    public string ImportProfile { get; init; } = string.Empty;

    public string CsvExportUrl { get; init; } = string.Empty;
}

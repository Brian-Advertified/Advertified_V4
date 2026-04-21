namespace Advertified.App.Contracts.Leads;

public sealed class GoogleSheetsLeadIntegrationRunDto
{
    public string Operation { get; init; } = string.Empty;

    public int ProcessedSourceCount { get; init; }

    public int FailedSourceCount { get; init; }

    public int CreatedLeadCount { get; init; }

    public int UpdatedLeadCount { get; init; }

    public int ExportedItemCount { get; init; }

    public string Message { get; init; } = string.Empty;
}

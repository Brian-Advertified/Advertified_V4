namespace Advertified.App.Contracts.Leads;

public sealed class ImportLeadCsvRequest
{
    public string CsvText { get; init; } = string.Empty;

    public string DefaultSource { get; init; } = "csv_import";

    public string ImportProfile { get; init; } = "standard";
}

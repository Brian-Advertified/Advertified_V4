namespace Advertified.App.Configuration;

public sealed class GoogleSheetsLeadOpsOptions
{
    public const string SectionName = "GoogleSheetsLeadOps";

    public bool Enabled { get; set; }

    public bool ImportEnabled { get; set; } = true;

    public bool ExportEnabled { get; set; } = true;

    public int ImportPollIntervalMinutes { get; set; } = 15;

    public int ExportPollIntervalMinutes { get; set; } = 5;

    public string ExportWebhookUrl { get; set; } = string.Empty;

    public string ExportWebhookAuthHeaderName { get; set; } = "X-Advertified-Token";

    public string ExportWebhookAuthToken { get; set; } = string.Empty;

    public List<GoogleSheetsLeadSourceOptions> IntakeSources { get; set; } = new();
}

public sealed class GoogleSheetsLeadSourceOptions
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string CsvExportUrl { get; set; } = string.Empty;

    public string DefaultSource { get; set; } = "google_sheet";

    public string ImportProfile { get; set; } = "standard";
}

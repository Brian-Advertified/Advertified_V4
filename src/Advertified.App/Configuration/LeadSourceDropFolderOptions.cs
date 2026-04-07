namespace Advertified.App.Configuration;

public sealed class LeadSourceDropFolderOptions
{
    public const string SectionName = "LeadSourceDropFolder";

    public bool Enabled { get; set; }

    public string InboxPath { get; set; } = "App_Data/lead-imports/inbox";

    public string ProcessedPath { get; set; } = "App_Data/lead-imports/processed";

    public string FailedPath { get; set; } = "App_Data/lead-imports/failed";

    public int PollIntervalSeconds { get; set; } = 60;

    public string DefaultSource { get; set; } = "csv_drop";

    public string DefaultImportProfile { get; set; } = "standard";

    public bool AnalyzeImportedLeads { get; set; } = true;
}

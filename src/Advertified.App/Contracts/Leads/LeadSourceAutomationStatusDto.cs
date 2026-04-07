namespace Advertified.App.Contracts.Leads;

public sealed class LeadSourceAutomationStatusDto
{
    public bool DropFolderEnabled { get; init; }

    public string InboxPath { get; init; } = string.Empty;

    public string ProcessedPath { get; init; } = string.Empty;

    public string FailedPath { get; init; } = string.Empty;

    public int PendingFileCount { get; init; }

    public int ProcessedFileCount { get; init; }

    public int FailedFileCount { get; init; }

    public string DefaultSource { get; init; } = string.Empty;

    public string DefaultImportProfile { get; init; } = string.Empty;

    public bool AnalyzeImportedLeads { get; init; }
}

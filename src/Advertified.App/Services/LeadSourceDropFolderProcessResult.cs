namespace Advertified.App.Services;

public sealed class LeadSourceDropFolderProcessResult
{
    public int ProcessedFileCount { get; init; }

    public int FailedFileCount { get; init; }

    public int ImportedLeadCount { get; init; }

    public int AnalyzedLeadCount { get; init; }
}

namespace Advertified.App.Contracts.Leads;

public sealed class LeadSourceAutomationRunDto
{
    public int ProcessedFileCount { get; init; }

    public int FailedFileCount { get; init; }

    public int ImportedLeadCount { get; init; }

    public int AnalyzedLeadCount { get; init; }
}

namespace Advertified.App.Contracts.Leads;

public sealed class LeadPaidMediaSyncStatusDto
{
    public bool Enabled { get; init; }

    public int BatchSize { get; init; }

    public int IntervalMinutes { get; init; }

    public LeadPaidMediaSyncRunDto? LastRun { get; init; }
}

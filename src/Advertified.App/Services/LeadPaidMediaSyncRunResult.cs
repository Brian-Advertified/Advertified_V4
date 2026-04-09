namespace Advertified.App.Services;

public sealed class LeadPaidMediaSyncRunResult
{
    public DateTime StartedAtUtc { get; init; }

    public DateTime FinishedAtUtc { get; init; }

    public bool Skipped { get; init; }

    public string? SkipReason { get; init; }

    public int TotalLeadCount { get; init; }

    public int ProcessedLeadCount { get; init; }

    public int FailedLeadCount { get; init; }

    public int EvidenceRowCount { get; init; }

    public IReadOnlyList<string> EnabledProviders { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, int> ProviderEvidenceCounts { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

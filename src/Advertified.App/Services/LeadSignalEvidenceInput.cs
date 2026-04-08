namespace Advertified.App.Services;

public sealed class LeadSignalEvidenceInput
{
    public string Channel { get; init; } = string.Empty;

    public string SignalType { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Confidence { get; init; } = "weakly_inferred";

    public int Weight { get; init; }

    public decimal ReliabilityMultiplier { get; init; } = 1.0m;

    public bool IsPositive { get; init; } = true;

    public DateTime? ObservedAtUtc { get; init; }

    public string? EvidenceUrl { get; init; }

    public string Value { get; init; } = string.Empty;
}

namespace Advertified.App.Services;

public sealed class LeadBusinessProfile
{
    public string BusinessType { get; init; } = string.Empty;

    public string PrimaryLocation { get; init; } = string.Empty;

    public string TargetAudience { get; init; } = string.Empty;

    public string GenderFocus { get; init; } = string.Empty;

    public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();

    public decimal ConfidenceScore { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<LeadEvidenceFieldTrace> EvidenceTrace { get; init; } = Array.Empty<LeadEvidenceFieldTrace>();
}

public sealed class LeadEvidenceFieldTrace
{
    public string Field { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Confidence { get; init; } = "unknown";

    public string Source { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

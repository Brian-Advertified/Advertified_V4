namespace Advertified.App.Contracts.Leads;

public sealed class LeadBusinessProfileDto
{
    public string BusinessType { get; init; } = string.Empty;

    public string PrimaryLocation { get; init; } = string.Empty;

    public string TargetAudience { get; init; } = string.Empty;

    public string GenderFocus { get; init; } = string.Empty;

    public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();

    public decimal ConfidenceScore { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<LeadEvidenceFieldTraceDto> EvidenceTrace { get; init; } = Array.Empty<LeadEvidenceFieldTraceDto>();
}

public sealed class LeadEvidenceFieldTraceDto
{
    public string Field { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Confidence { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

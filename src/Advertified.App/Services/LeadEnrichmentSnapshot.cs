namespace Advertified.App.Services;

public sealed class LeadEnrichmentSnapshot
{
    public IReadOnlyList<LeadEnrichmentField> Fields { get; init; } = Array.Empty<LeadEnrichmentField>();

    public LeadConfidenceGate ConfidenceGate { get; init; } = new();

    public decimal ConfidenceScore { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class LeadEnrichmentField
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Confidence { get; init; } = "unknown";

    public string Source { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public bool Required { get; init; }
}

public sealed class LeadConfidenceGate
{
    public bool IsBlocked { get; init; }

    public IReadOnlyList<string> RequiredFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingRequiredFields { get; init; } = Array.Empty<string>();

    public string Message { get; init; } = string.Empty;
}

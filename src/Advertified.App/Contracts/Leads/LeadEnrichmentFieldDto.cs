namespace Advertified.App.Contracts.Leads;

public sealed class LeadEnrichmentFieldDto
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Confidence { get; init; } = "unknown";

    public string Source { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public bool Required { get; init; }
}

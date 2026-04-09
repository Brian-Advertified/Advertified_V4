namespace Advertified.App.Contracts.Leads;

public sealed class LeadEnrichmentSnapshotDto
{
    public IReadOnlyList<LeadEnrichmentFieldDto> Fields { get; init; } = Array.Empty<LeadEnrichmentFieldDto>();

    public LeadConfidenceGateDto ConfidenceGate { get; init; } = new();

    public decimal ConfidenceScore { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public DateTime GeneratedAtUtc { get; init; }
}

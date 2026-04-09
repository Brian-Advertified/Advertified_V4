using System;

namespace Advertified.App.Contracts.Leads;

public sealed class LeadDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Website { get; init; }

    public string Location { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string? SourceReference { get; init; }

    public IReadOnlyList<string> AutoInferredFields { get; init; } = Array.Empty<string>();

    public DateTime? LastDiscoveredAt { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public DateTime CreatedAt { get; init; }
}

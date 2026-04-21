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

    public Guid? OwnerAgentUserId { get; init; }

    public string? OwnerAgentName { get; init; }

    public IReadOnlyList<string> AutoInferredFields { get; init; } = Array.Empty<string>();

    public DateTime? LastDiscoveredAt { get; init; }

    public DateTime? FirstContactedAt { get; init; }

    public DateTime? LastContactedAt { get; init; }

    public DateTime? NextFollowUpAt { get; init; }

    public DateTime? SlaDueAt { get; init; }

    public string? LastOutcome { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public DateTime CreatedAt { get; init; }
}

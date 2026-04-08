using System;

namespace Advertified.App.Data.Entities;

public sealed class LeadSignalEvidence
{
    public long Id { get; set; }

    public int LeadId { get; set; }

    public int SignalId { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string SignalType { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Confidence { get; set; } = string.Empty;

    public int Weight { get; set; }

    public decimal ReliabilityMultiplier { get; set; }

    public decimal FreshnessMultiplier { get; set; }

    public decimal EffectiveWeight { get; set; }

    public bool IsPositive { get; set; } = true;

    public DateTime? ObservedAt { get; set; }

    public string? EvidenceUrl { get; set; }

    public string Value { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Lead Lead { get; set; } = null!;

    public Signal Signal { get; set; } = null!;
}

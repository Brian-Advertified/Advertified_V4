namespace Advertified.App.Services;

public sealed class LeadChannelSignalEvidence
{
    public string Type { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public int Weight { get; init; }

    public decimal ReliabilityMultiplier { get; init; }

    public decimal FreshnessMultiplier { get; init; }

    public decimal EffectiveWeight { get; init; }

    public string Value { get; init; } = string.Empty;
}

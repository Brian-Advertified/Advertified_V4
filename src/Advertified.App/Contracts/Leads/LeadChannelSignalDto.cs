namespace Advertified.App.Contracts.Leads;

public sealed class LeadChannelSignalDto
{
    public string Type { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public int Weight { get; init; }

    public decimal ReliabilityMultiplier { get; init; }

    public decimal FreshnessMultiplier { get; init; }

    public int EffectiveWeight { get; init; }

    public string Value { get; init; } = string.Empty;
}

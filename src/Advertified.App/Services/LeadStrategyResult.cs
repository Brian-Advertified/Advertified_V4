namespace Advertified.App.Services;

public sealed class LeadStrategyResult
{
    public string Archetype { get; init; } = string.Empty;

    public string Objective { get; init; } = string.Empty;

    public IReadOnlyList<LeadStrategyChannelPlan> Channels { get; init; } = Array.Empty<LeadStrategyChannelPlan>();

    public IReadOnlyList<string> GeoTargets { get; init; } = Array.Empty<string>();

    public string Timing { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;
}

public sealed class LeadStrategyChannelPlan
{
    public string Channel { get; init; } = string.Empty;

    public int BudgetSharePercent { get; init; }

    public string Reason { get; init; } = string.Empty;
}

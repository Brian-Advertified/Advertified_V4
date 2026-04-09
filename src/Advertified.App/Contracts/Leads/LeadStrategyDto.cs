namespace Advertified.App.Contracts.Leads;

public sealed class LeadStrategyDto
{
    public string Archetype { get; init; } = string.Empty;

    public string Objective { get; init; } = string.Empty;

    public IReadOnlyList<LeadStrategyChannelDto> Channels { get; init; } = Array.Empty<LeadStrategyChannelDto>();

    public IReadOnlyList<string> GeoTargets { get; init; } = Array.Empty<string>();

    public string Timing { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;
}

public sealed class LeadStrategyChannelDto
{
    public string Channel { get; init; } = string.Empty;

    public int BudgetSharePercent { get; init; }

    public string Reason { get; init; } = string.Empty;
}

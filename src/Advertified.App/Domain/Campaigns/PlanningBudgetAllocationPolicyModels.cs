namespace Advertified.App.Domain.Campaigns;

public sealed class PlanningBudgetAllocationPolicySnapshot
{
    public IReadOnlyList<ChannelAllocationPolicyRule> ChannelRules { get; init; } = Array.Empty<ChannelAllocationPolicyRule>();
    public IReadOnlyList<GeoAllocationPolicyRule> GeoRules { get; init; } = Array.Empty<GeoAllocationPolicyRule>();
}

public sealed class ChannelAllocationPolicyRule
{
    public string PolicyKey { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string? Objective { get; init; }
    public string? AudienceSegment { get; init; }
    public string? GeographyScope { get; init; }
    public decimal? MinBudget { get; init; }
    public decimal? MaxBudget { get; init; }
    public Dictionary<string, decimal> Weights { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GeoAllocationPolicyRule
{
    public string PolicyKey { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string? Objective { get; init; }
    public string? AudienceSegment { get; init; }
    public string? GeographyScope { get; init; }
    public decimal? MinBudget { get; init; }
    public decimal? MaxBudget { get; init; }
    public double? NearbyRadiusKm { get; init; }
    public Dictionary<string, decimal> Weights { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

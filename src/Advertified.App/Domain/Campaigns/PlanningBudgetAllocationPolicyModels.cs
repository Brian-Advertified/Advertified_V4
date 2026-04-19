namespace Advertified.App.Domain.Campaigns;

public sealed class PlanningBudgetAllocationPolicySnapshot
{
    public IReadOnlyList<BudgetBandAllocationPolicyRule> BudgetBands { get; init; } = Array.Empty<BudgetBandAllocationPolicyRule>();
    public PlanningAllocationGlobalRules GlobalRules { get; init; } = new();
    public IReadOnlyList<GeoAllocationPolicyRule> GeoRules { get; init; } = Array.Empty<GeoAllocationPolicyRule>();
}

public sealed class BudgetBandAllocationPolicyRule
{
    public string Name { get; init; } = string.Empty;
    public decimal Min { get; init; }
    public decimal Max { get; init; }
    public decimal OohTarget { get; init; }
    public decimal BillboardShareOfOoh { get; init; } = 0.65m;
    public decimal TvMin { get; init; }
    public bool TvEligible { get; init; }
    public decimal[] RadioRange { get; init; } = Array.Empty<decimal>();
    public decimal[] DigitalRange { get; init; } = Array.Empty<decimal>();
}

public sealed class PlanningAllocationGlobalRules
{
    public decimal MaxOoh { get; init; } = 0.50m;
    public decimal MinDigital { get; init; } = 0.15m;
    public bool EnforceTvFloorIfPreferred { get; init; } = true;
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

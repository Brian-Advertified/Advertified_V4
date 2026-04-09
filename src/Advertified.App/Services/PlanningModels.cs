using Advertified.App.Services.Abstractions;
using System.Text.Json;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services;

public readonly record struct PlanningCandidateAnalysis(
    decimal Score,
    string[] SelectionReasons,
    string[] PolicyFlags,
    decimal ConfidenceScore);

public readonly record struct PlanningPolicyOutcome(
    List<InventoryCandidate> Candidates,
    IReadOnlyList<string> FallbackFlags,
    IReadOnlyList<PlanningCandidateRejection> Rejections);

public readonly record struct PlanningCandidateRejection(
    string Stage,
    string Reason,
    Guid SourceId,
    string DisplayName,
    string MediaType);

public sealed class OohPlanningInventoryRow
{
    public Guid SourceId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string? Subtype { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? Suburb { get; set; }
    public string? Area { get; set; }
    public string? Language { get; set; }
    public int? LsmMin { get; set; }
    public int? LsmMax { get; set; }
    public decimal Cost { get; set; }
    public bool IsAvailable { get; set; }
    public bool PackageOnly { get; set; }
    public string? TimeBand { get; set; }
    public string? DayType { get; set; }
    public string? SlotType { get; set; }
    public int? DurationSeconds { get; set; }
    public string? RegionClusterCode { get; set; }
    public string? MarketScope { get; set; }
    public string? MarketTier { get; set; }
    public int? MonthlyListenership { get; set; }
    public bool IsFlagshipStation { get; set; }
    public bool IsPremiumStation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class BroadcastPlanningInventorySeed
{
    public BroadcastInventoryRecord Record { get; init; } = null!;
    public Guid SourceId { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SlotType { get; init; } = string.Empty;
    public decimal Cost { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}

public sealed class BroadcastPackageElementCandidate
{
    public string Name { get; set; } = string.Empty;
    public decimal? InvestmentZar { get; set; }
    public decimal? PackageCostZar { get; set; }
    public decimal? CostPerMonthZar { get; set; }
    public string? Notes { get; set; }
}

public sealed class BroadcastRateValueCandidate
{
    public string GroupName { get; set; } = string.Empty;
    public string SlotLabel { get; set; } = string.Empty;
    public decimal RateZar { get; set; }
}

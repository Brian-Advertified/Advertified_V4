namespace Advertified.App.Domain.Campaigns;

public sealed class InventoryCandidate
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
    public decimal Score { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();
}

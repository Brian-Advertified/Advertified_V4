namespace Advertified.App.Contracts.Campaigns;

public sealed class RecommendationItemResponse
{
    public Guid Id { get; set; }
    public string? SourceInventoryId { get; set; }
    public string? Region { get; set; }
    public string? Language { get; set; }
    public string? ShowDaypart { get; set; }
    public string? TimeBand { get; set; }
    public string? SlotType { get; set; }
    public string? Duration { get; set; }
    public string? Restrictions { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public IReadOnlyList<string> SelectionReasons { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PolicyFlags { get; set; } = Array.Empty<string>();
    public int Quantity { get; set; }
    public string? Flighting { get; set; }
    public string? ItemNotes { get; set; }
    public string? Dimensions { get; set; }
    public string? Material { get; set; }
    public string? Illuminated { get; set; }
    public string? TrafficCount { get; set; }
    public string? SiteNumber { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Type { get; set; } = "base";
}

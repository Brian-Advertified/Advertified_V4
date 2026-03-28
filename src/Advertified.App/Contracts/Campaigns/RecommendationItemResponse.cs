namespace Advertified.App.Contracts.Campaigns;

public sealed class RecommendationItemResponse
{
    public Guid Id { get; set; }
    public string? SourceInventoryId { get; set; }
    public int Quantity { get; set; }
    public string? Flighting { get; set; }
    public string? ItemNotes { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Type { get; set; } = "base";
}

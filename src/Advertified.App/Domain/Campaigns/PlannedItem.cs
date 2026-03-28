namespace Advertified.App.Domain.Campaigns;

public sealed class PlannedItem
{
    public Guid SourceId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal TotalCost => UnitCost * Quantity;
    public decimal Score { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();
}

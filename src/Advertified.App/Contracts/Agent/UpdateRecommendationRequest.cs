namespace Advertified.App.Contracts.Agent;

public sealed class UpdateRecommendationRequest
{
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyList<SelectedInventoryItemRequest> InventoryItems { get; set; } = Array.Empty<SelectedInventoryItemRequest>();
}

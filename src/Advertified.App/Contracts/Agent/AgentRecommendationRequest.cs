namespace Advertified.App.Contracts.Agent;

public sealed class AgentRecommendationRequest
{
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyList<SelectedInventoryItemRequest> InventoryItems { get; set; } = Array.Empty<SelectedInventoryItemRequest>();
}

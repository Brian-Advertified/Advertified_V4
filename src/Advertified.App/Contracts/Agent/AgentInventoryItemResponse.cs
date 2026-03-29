namespace Advertified.App.Contracts.Agent;

public sealed class AgentInventoryItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ShowDaypart { get; set; } = string.Empty;
    public string TimeBand { get; set; } = string.Empty;
    public string SlotType { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string Restrictions { get; set; } = string.Empty;
}

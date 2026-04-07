namespace Advertified.App.Contracts.Leads;

public sealed class LeadActionInboxItemDto
{
    public int ActionId { get; init; }

    public int LeadId { get; init; }

    public string LeadName { get; init; } = string.Empty;

    public string LeadLocation { get; init; } = string.Empty;

    public string LeadCategory { get; init; } = string.Empty;

    public string LeadSource { get; init; } = string.Empty;

    public LeadActionDto Action { get; init; } = new();
}

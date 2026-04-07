namespace Advertified.App.Contracts.Leads;

public sealed class LeadInteractionDto
{
    public int Id { get; init; }

    public int LeadId { get; init; }

    public int? LeadActionId { get; init; }

    public string InteractionType { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

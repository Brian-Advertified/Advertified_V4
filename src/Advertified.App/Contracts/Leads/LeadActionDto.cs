namespace Advertified.App.Contracts.Leads;

public sealed class LeadActionDto
{
    public int Id { get; init; }

    public int LeadId { get; init; }

    public int? LeadInsightId { get; init; }

    public string ActionType { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Priority { get; init; } = string.Empty;

    public Guid? AssignedAgentUserId { get; init; }

    public string? AssignedAgentName { get; init; }

    public DateTime? AssignedAt { get; init; }

    public bool IsAssignedToCurrentUser { get; init; }

    public bool IsUnassigned { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}

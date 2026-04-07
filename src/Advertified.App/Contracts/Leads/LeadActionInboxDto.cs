namespace Advertified.App.Contracts.Leads;

public sealed class LeadActionInboxDto
{
    public int TotalOpenActions { get; init; }

    public int AssignedToMeCount { get; init; }

    public int UnassignedCount { get; init; }

    public int HighPriorityCount { get; init; }

    public IReadOnlyList<LeadActionInboxItemDto> Items { get; init; } = Array.Empty<LeadActionInboxItemDto>();
}

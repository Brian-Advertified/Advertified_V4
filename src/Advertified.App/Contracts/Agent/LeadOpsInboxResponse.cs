namespace Advertified.App.Contracts.Agent;

public sealed class LeadOpsInboxResponse
{
    public int TotalItems { get; set; }

    public int UrgentCount { get; set; }

    public int AssignedToMeCount { get; set; }

    public int UnassignedCount { get; set; }

    public int NewInboundProspectsCount { get; set; }

    public int UnassignedProspectsCount { get; set; }

    public int OpenLeadActionsCount { get; set; }

    public int NoRecentActivityCount { get; set; }

    public int AwaitingClientResponsesCount { get; set; }

    public int OverdueFollowUpsCount { get; set; }

    public IReadOnlyList<LeadOpsInboxItemResponse> Items { get; set; } = Array.Empty<LeadOpsInboxItemResponse>();
}

public sealed class LeadOpsInboxItemResponse
{
    public string Id { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string ItemLabel { get; set; } = string.Empty;

    public Guid? CampaignId { get; set; }

    public Guid? ProspectLeadId { get; set; }

    public int? LeadId { get; set; }

    public int? LeadActionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string UnifiedStatus { get; set; } = string.Empty;

    public Guid? AssignedAgentUserId { get; set; }

    public string? AssignedAgentName { get; set; }

    public bool IsAssignedToCurrentUser { get; set; }

    public bool IsUnassigned { get; set; }

    public bool IsUrgent { get; set; }

    public string RoutePath { get; set; } = string.Empty;

    public string RouteLabel { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }
}

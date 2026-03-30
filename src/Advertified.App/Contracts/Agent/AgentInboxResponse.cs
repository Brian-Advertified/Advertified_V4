namespace Advertified.App.Contracts.Agent;

public sealed class AgentInboxResponse
{
    public int TotalCampaigns { get; set; }
    public int AssignedToMeCount { get; set; }
    public int UnassignedCount { get; set; }
    public int UrgentCount { get; set; }
    public int ManualReviewCount { get; set; }
    public int OverBudgetCount { get; set; }
    public int StaleCount { get; set; }
    public int NewlyPaidCount { get; set; }
    public int BriefWaitingCount { get; set; }
    public int PlanningReadyCount { get; set; }
    public int AgentReviewCount { get; set; }
    public int ReadyToSendCount { get; set; }
    public int WaitingOnClientCount { get; set; }
    public int CompletedCount { get; set; }
    public IReadOnlyList<AgentInboxItemResponse> Items { get; set; } = Array.Empty<AgentInboxItemResponse>();
}

public sealed class AgentInboxItemResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageBandName { get; set; } = string.Empty;
    public Guid PackageBandId { get; set; }
    public decimal SelectedBudget { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PlanningMode { get; set; }
    public string QueueStage { get; set; } = string.Empty;
    public string QueueLabel { get; set; } = string.Empty;
    public Guid? AssignedAgentUserId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public bool IsAssignedToCurrentUser { get; set; }
    public bool IsUnassigned { get; set; }
    public string NextAction { get; set; } = string.Empty;
    public bool ManualReviewRequired { get; set; }
    public bool IsOverBudget { get; set; }
    public bool IsStale { get; set; }
    public bool IsUrgent { get; set; }
    public int AgeInDays { get; set; }
    public bool HasBrief { get; set; }
    public bool HasRecommendation { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

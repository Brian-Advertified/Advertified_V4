namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignListItemResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public string? BusinessName { get; set; }
    public Guid PackageOrderId { get; set; }
    public Guid PackageBandId { get; set; }
    public string PackageBandName { get; set; } = string.Empty;
    public decimal SelectedBudget { get; set; }
    public string PaymentProvider { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? CampaignName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PlanningMode { get; set; }
    public bool AiUnlocked { get; set; }
    public bool AgentAssistanceRequested { get; set; }
    public Guid? AssignedAgentUserId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public bool IsAssignedToCurrentUser { get; set; }
    public bool IsUnassigned { get; set; }
    public string NextAction { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

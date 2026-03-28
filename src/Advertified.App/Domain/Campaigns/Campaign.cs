namespace Advertified.App.Domain.Campaigns;

public sealed class Campaign
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PackageOrderId { get; set; }
    public Guid PackageBandId { get; set; }
    public string? CampaignName { get; set; }
    public string Status { get; set; } = "paid";
    public string? PlanningMode { get; set; }
    public bool AiUnlocked { get; set; }
    public bool AgentAssistanceRequested { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

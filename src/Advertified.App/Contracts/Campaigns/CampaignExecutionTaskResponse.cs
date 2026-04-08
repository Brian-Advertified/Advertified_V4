namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignExecutionTaskResponse
{
    public Guid Id { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

namespace Advertified.App.Data.Entities;

public sealed class CampaignExecutionTask
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = "open";
    public int SortOrder { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
}

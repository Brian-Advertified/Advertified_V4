using System;

namespace Advertified.App.Data.Entities;

public class LeadAction
{
    public int Id { get; set; }

    public int LeadId { get; set; }

    public int? LeadInsightId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "open";

    public string Priority { get; set; } = "medium";

    public Guid? AssignedAgentUserId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual UserAccount? AssignedAgentUser { get; set; }

    public virtual ICollection<LeadInteraction> Interactions { get; set; } = new List<LeadInteraction>();

    public virtual Lead Lead { get; set; } = null!;

    public virtual LeadInsight? LeadInsight { get; set; }
}

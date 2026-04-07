using System;

namespace Advertified.App.Data.Entities;

public class LeadInteraction
{
    public int Id { get; set; }

    public int LeadId { get; set; }

    public int? LeadActionId { get; set; }

    public string InteractionType { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public virtual Lead Lead { get; set; } = null!;

    public virtual LeadAction? LeadAction { get; set; }
}

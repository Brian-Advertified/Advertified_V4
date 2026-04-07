using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public class Lead
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Website { get; set; }

    public string Location { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Source { get; set; } = "manual";

    public string? SourceReference { get; set; }

    public DateTime? LastDiscoveredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<LeadAction> Actions { get; set; } = new List<LeadAction>();

    public virtual ICollection<LeadInteraction> Interactions { get; set; } = new List<LeadInteraction>();

    public virtual ICollection<LeadInsight> Insights { get; set; } = new List<LeadInsight>();

    public virtual ICollection<Signal> Signals { get; set; } = new List<Signal>();
}

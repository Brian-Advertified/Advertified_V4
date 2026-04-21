using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class Lead
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Website { get; set; }

    public string Location { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Source { get; set; } = "manual";

    public string? SourceReference { get; set; }

    public DateTime? LastDiscoveredAt { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<LeadAction> Actions { get; set; } = new List<LeadAction>();

    public virtual ICollection<LeadInteraction> Interactions { get; set; } = new List<LeadInteraction>();

    public virtual ICollection<LeadInsight> Insights { get; set; } = new List<LeadInsight>();

    public virtual ICollection<LeadSignalEvidence> SignalEvidences { get; set; } = new List<LeadSignalEvidence>();

    public virtual ICollection<Signal> Signals { get; set; } = new List<Signal>();
}

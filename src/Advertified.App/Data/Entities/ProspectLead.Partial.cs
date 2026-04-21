using System;

namespace Advertified.App.Data.Entities;

public partial class ProspectLead
{
    public int? SourceLeadId { get; set; }

    public DateTime? LastContactedAt { get; set; }

    public DateTime? NextFollowUpAt { get; set; }

    public DateTime? SlaDueAt { get; set; }

    public string? LastOutcome { get; set; }

    public virtual Lead? SourceLead { get; set; }
}

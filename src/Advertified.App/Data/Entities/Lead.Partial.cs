using System;

namespace Advertified.App.Data.Entities;

public partial class Lead
{
    public Guid? OwnerAgentUserId { get; set; }

    public DateTime? FirstContactedAt { get; set; }

    public DateTime? LastContactedAt { get; set; }

    public DateTime? NextFollowUpAt { get; set; }

    public DateTime? SlaDueAt { get; set; }

    public string? LastOutcome { get; set; }

    public virtual UserAccount? OwnerAgentUser { get; set; }
}

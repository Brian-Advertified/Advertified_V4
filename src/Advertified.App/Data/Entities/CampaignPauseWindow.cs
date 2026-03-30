using System;

namespace Advertified.App.Data.Entities;

public partial class CampaignPauseWindow
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ResumedByUserId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? PauseReason { get; set; }

    public string? ResumeReason { get; set; }

    public int PausedDayCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual UserAccount? ResumedByUser { get; set; }
}

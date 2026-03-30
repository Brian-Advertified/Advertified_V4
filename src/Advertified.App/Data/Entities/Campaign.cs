using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class Campaign
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PackageOrderId { get; set; }

    public Guid PackageBandId { get; set; }

    public string? CampaignName { get; set; }

    public string Status { get; set; } = null!;

    public string? PlanningMode { get; set; }

    public bool AiUnlocked { get; set; }

    public bool AgentAssistanceRequested { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? PausedAt { get; set; }

    public int TotalPausedDays { get; set; }

    public string? PauseReason { get; set; }

    public virtual CampaignBrief? CampaignBrief { get; set; }

    public virtual CampaignBriefDraft? CampaignBriefDraft { get; set; }

    public virtual CampaignConversation? CampaignConversation { get; set; }

    public virtual ICollection<CampaignRecommendation> CampaignRecommendations { get; set; } = new List<CampaignRecommendation>();

    public virtual PackageBand PackageBand { get; set; } = null!;

    public virtual PackageOrder PackageOrder { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}

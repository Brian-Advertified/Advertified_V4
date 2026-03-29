namespace Advertified.App.Data.Entities;

public partial class Campaign
{
    public Guid? AssignedAgentUserId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime? AssignmentEmailSentAt { get; set; }

    public DateTime? AgentWorkStartedEmailSentAt { get; set; }

    public DateTime? RecommendationReadyEmailSentAt { get; set; }

    public virtual UserAccount? AssignedAgentUser { get; set; }
}

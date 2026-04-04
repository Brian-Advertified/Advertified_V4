namespace Advertified.App.Support;

public static class CampaignStatuses
{
    public const string AwaitingPurchase = "awaiting_purchase";
    public const string Paid = "paid";
    public const string BriefInProgress = "brief_in_progress";
    public const string BriefSubmitted = "brief_submitted";
    public const string PlanningInProgress = "planning_in_progress";
    public const string ReviewReady = "review_ready";
    public const string Approved = "approved";
    public const string CreativeSentToClientForApproval = "creative_sent_to_client_for_approval";
    public const string CreativeChangesRequested = "creative_changes_requested";
    public const string CreativeApproved = "creative_approved";
    public const string BookingInProgress = "booking_in_progress";
    public const string Launched = "launched";
}

public static class RecommendationStatuses
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string SentToClient = "sent_to_client";
}

public static class ConversationParticipantRoles
{
    public const string Client = "client";
    public const string Agent = "agent";
}

public static class QueueStages
{
    public const string NewlyPaid = "newly_paid";
    public const string BriefWaiting = "brief_waiting";
    public const string PlanningReady = "planning_ready";
    public const string AgentReview = "agent_review";
    public const string ReadyToSend = "ready_to_send";
    public const string WaitingOnClient = "waiting_on_client";
    public const string Completed = "completed";
    public const string Watching = "watching";
}

public static class TimelineStates
{
    public const string Complete = "complete";
    public const string Current = "current";
    public const string Upcoming = "upcoming";
}

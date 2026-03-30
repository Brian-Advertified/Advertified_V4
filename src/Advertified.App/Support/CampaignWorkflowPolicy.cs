using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class CampaignWorkflowPolicy
{
    public static string GetClientNextAction(Campaign campaign)
    {
        return campaign.Status switch
        {
            CampaignStatuses.Paid => "Complete your campaign brief",
            CampaignStatuses.BriefInProgress => "Finish and submit your brief",
            CampaignStatuses.BriefSubmitted => "Choose planning mode",
            CampaignStatuses.PlanningInProgress => "Review your tailored recommendation",
            CampaignStatuses.ReviewReady => "Approve or request updates",
            CampaignStatuses.Approved => "Creative production is in progress",
            CampaignStatuses.CreativeChangesRequested => "Creative revisions are in progress",
            CampaignStatuses.CreativeSentToClientForApproval => "Review the finished media for final approval",
            CampaignStatuses.CreativeApproved => "Final approval is captured and activation is being prepared",
            CampaignStatuses.Launched => "Campaign is now live",
            _ => "Continue campaign setup"
        };
    }

    public static IReadOnlyList<CampaignTimelineStepResponse> BuildTimeline(Campaign campaign)
    {
        var latestRecommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var recommendationStatus = latestRecommendation?.Status?.Trim().ToLowerInvariant();

        var paymentComplete = campaign.Status is not "awaiting_purchase";
        var briefComplete = campaign.Status is not CampaignStatuses.Paid and not CampaignStatuses.BriefInProgress || campaign.CampaignBrief?.SubmittedAt is not null;
        var recommendationReady = campaign.Status is CampaignStatuses.PlanningInProgress or CampaignStatuses.ReviewReady or CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested or CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.Launched || latestRecommendation is not null;
        var clientReviewActive = campaign.Status is CampaignStatuses.ReviewReady || recommendationStatus == RecommendationStatuses.SentToClient;
        var recommendationApproved = campaign.Status is CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested or CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.Launched || recommendationStatus == RecommendationStatuses.Approved;
        var creativeProductionStarted = campaign.Status is CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested or CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.Launched;
        var creativeReviewActive = campaign.Status == CampaignStatuses.CreativeSentToClientForApproval;
        var creativeApproved = campaign.Status is CampaignStatuses.CreativeApproved or CampaignStatuses.Launched;
        var launchActivated = campaign.Status == CampaignStatuses.Launched;

        return new[]
        {
            BuildTimelineStep(
                key: "payment",
                label: "Payment confirmed",
                description: "Your package payment has been received and your campaign is open.",
                isComplete: paymentComplete,
                isCurrent: campaign.Status == CampaignStatuses.Paid),
            BuildTimelineStep(
                key: "brief",
                label: "Brief submitted",
                description: "Your goals, audience, geography, and media preferences have been captured.",
                isComplete: briefComplete,
                isCurrent: campaign.Status == CampaignStatuses.BriefInProgress || campaign.Status == CampaignStatuses.BriefSubmitted),
            BuildTimelineStep(
                key: "recommendation",
                label: "Recommendation prepared",
                description: "Advertified has prepared a draft recommendation for your review.",
                isComplete: recommendationReady,
                isCurrent: campaign.Status == CampaignStatuses.PlanningInProgress),
            BuildTimelineStep(
                key: "review",
                label: "Client review",
                description: "Review the recommendation and approve it or request changes.",
                isComplete: recommendationApproved,
                isCurrent: clientReviewActive && !recommendationApproved),
            BuildTimelineStep(
                key: "creative-production",
                label: "Creative production",
                description: "Advertified's creative director is preparing your finished media from the approved recommendation.",
                isComplete: campaign.Status is CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.Launched,
                isCurrent: creativeProductionStarted && !creativeReviewActive && !creativeApproved),
            BuildTimelineStep(
                key: "creative-approval",
                label: "Final creative approval",
                description: "Your finished media has been sent back for final client approval.",
                isComplete: creativeApproved,
                isCurrent: creativeReviewActive),
            BuildTimelineStep(
                key: "launch-ready",
                label: "Launch ready",
                description: "Final creative approval has been captured and operations can now activate the campaign.",
                isComplete: launchActivated,
                isCurrent: creativeApproved && !launchActivated),
            BuildTimelineStep(
                key: "live",
                label: "Campaign live",
                description: "Operations has activated the campaign and it is now live.",
                isComplete: launchActivated,
                isCurrent: false)
        };
    }

    public static string ResolveAgentQueueStage(Campaign campaign)
    {
        var latestRecommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var hasRecommendation = latestRecommendation is not null;
        var recommendationStatus = latestRecommendation?.Status?.Trim().ToLowerInvariant();

        return campaign.Status switch
        {
            CampaignStatuses.Paid => QueueStages.NewlyPaid,
            CampaignStatuses.BriefInProgress => QueueStages.BriefWaiting,
            CampaignStatuses.BriefSubmitted => QueueStages.PlanningReady,
            _ when recommendationStatus == RecommendationStatuses.Approved => QueueStages.Completed,
            _ when recommendationStatus == RecommendationStatuses.SentToClient => QueueStages.WaitingOnClient,
            CampaignStatuses.PlanningInProgress when hasRecommendation => QueueStages.AgentReview,
            CampaignStatuses.PlanningInProgress => QueueStages.PlanningReady,
            CampaignStatuses.ReviewReady => QueueStages.WaitingOnClient,
            _ => QueueStages.Watching
        };
    }

    public static string GetAgentQueueLabel(string stage)
    {
        return stage switch
        {
            QueueStages.NewlyPaid => "Newly paid",
            QueueStages.BriefWaiting => "Brief in progress",
            QueueStages.PlanningReady => "Needs planning",
            QueueStages.AgentReview => "Needs agent review",
            QueueStages.ReadyToSend => "Ready to send",
            QueueStages.WaitingOnClient => "Waiting on client",
            QueueStages.Completed => "Completed",
            _ => "Watching"
        };
    }

    public static string GetAgentNextAction(Campaign campaign, string stage, Guid currentUserId)
    {
        if (string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase))
        {
            return "Final creative approval is captured. Mark the campaign live when activation starts.";
        }

        if (string.Equals(campaign.Status, CampaignStatuses.Launched, StringComparison.OrdinalIgnoreCase))
        {
            return "Campaign is live. Monitor delivery and support any execution follow-up.";
        }

        var latestRecommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var selectedBudget = PricingPolicy.ResolvePlanningBudget(
            campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            campaign.PackageOrder.AiStudioReserveAmount);
        var manualReviewRequired = ExtractManualReviewRequired(latestRecommendation?.Rationale);
        var isOverBudget = latestRecommendation is not null
            && latestRecommendation.TotalCost > selectedBudget
            && !string.Equals(latestRecommendation.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase);
        var assignmentPrefix = campaign.AssignedAgentUserId switch
        {
            null => "Unassigned. Claim this campaign and",
            var assignedAgentId when assignedAgentId == currentUserId => "Assigned to you. Next,",
            _ => $"Assigned to {campaign.AssignedAgentUser?.FullName ?? "another agent"}. Monitor and"
        };

        if (isOverBudget)
        {
            return $"{assignmentPrefix} bring the draft back within the paid budget before sending it onward.";
        }

        if (manualReviewRequired)
        {
            return $"{assignmentPrefix} review the fallback warnings carefully before sending this recommendation.";
        }

        return stage switch
        {
            QueueStages.NewlyPaid => $"{assignmentPrefix} check the order and wait for the client brief.",
            QueueStages.BriefWaiting => $"{assignmentPrefix} monitor the brief and step in if the client needs help.",
            QueueStages.PlanningReady => $"{assignmentPrefix} open the campaign and create the recommendation.",
            QueueStages.AgentReview => $"{assignmentPrefix} review the AI draft, adjust the plan, and approve it before sending.",
            QueueStages.ReadyToSend => $"{assignmentPrefix} review the recommendation and send it to the client.",
            QueueStages.WaitingOnClient => $"{assignmentPrefix} wait for client approval or update requests.",
            QueueStages.Completed => $"{assignmentPrefix} archive this work or support activation if needed.",
            _ => $"{assignmentPrefix} monitor campaign progress for {campaign.PackageBand.Name}."
        };
    }

    public static bool IsAgentQueueStageStale(string stage, DateTimeOffset updatedAt)
    {
        var age = DateTimeOffset.UtcNow - updatedAt;
        return stage switch
        {
            QueueStages.NewlyPaid => age.TotalDays >= 2,
            QueueStages.PlanningReady or QueueStages.AgentReview => age.TotalDays >= 2,
            QueueStages.WaitingOnClient => age.TotalDays >= 5,
            QueueStages.BriefWaiting => age.TotalDays >= 4,
            _ => age.TotalDays >= 7
        };
    }

    public static int GetAgentQueueRank(string stage)
    {
        return stage switch
        {
            QueueStages.NewlyPaid => 0,
            QueueStages.PlanningReady => 1,
            QueueStages.AgentReview => 2,
            QueueStages.ReadyToSend => 3,
            QueueStages.BriefWaiting => 4,
            QueueStages.WaitingOnClient => 5,
            QueueStages.Completed => 6,
            _ => 7
        };
    }

    private static CampaignTimelineStepResponse BuildTimelineStep(
        string key,
        string label,
        string description,
        bool isComplete,
        bool isCurrent)
    {
        return new CampaignTimelineStepResponse
        {
            Key = key,
            Label = label,
            Description = description,
            State = isComplete ? TimelineStates.Complete : isCurrent ? TimelineStates.Current : TimelineStates.Upcoming
        };
    }

    private static bool ExtractManualReviewRequired(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return false;
        }

        var line = rationale
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .LastOrDefault(entry => entry.StartsWith("Manual review required:", StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return false;
        }

        var rawValue = line["Manual review required:".Length..].Trim();
        return bool.TryParse(rawValue, out var parsed) && parsed;
    }
}

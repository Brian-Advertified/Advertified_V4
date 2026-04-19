using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class CampaignWorkflowPolicy
{
    private const string PaymentStateCleared = "cleared";
    private const string PaymentStateManualReview = "manual_review";
    private const string PaymentStateRequired = "payment_required";

    public static string GetClientNextAction(Campaign campaign)
    {
        var recommendation = RecommendationSelectionPolicy.GetVisibleRecommendation(campaign);
        var isProspectiveCampaign = ProspectCampaignPolicy.IsProspectiveCampaign(campaign);
        var recommendationAwaitingDecision = IsRecommendationAwaitingDecision(campaign, recommendation);

        return campaign.Status switch
        {
            CampaignStatuses.AwaitingPurchase when recommendationAwaitingDecision => "Review your proposal and choose how to proceed",
            CampaignStatuses.AwaitingPurchase when isProspectiveCampaign => "Wait for your tailored proposal",
            CampaignStatuses.Paid => "Complete your campaign brief",
            CampaignStatuses.BriefInProgress => "Finish and submit your brief",
            CampaignStatuses.BriefSubmitted => "Choose planning mode",
            CampaignStatuses.PlanningInProgress => "Review your tailored recommendation",
            CampaignStatuses.ReviewReady => "Approve or request updates",
            CampaignStatuses.Approved => "Creative production is in progress",
            CampaignStatuses.CreativeChangesRequested => "Creative revisions are in progress",
            CampaignStatuses.CreativeSentToClientForApproval => "Review the finished media for final approval",
            CampaignStatuses.CreativeApproved => "Final creative approval is captured",
            CampaignStatuses.BookingInProgress => "Supplier booking is in progress",
            CampaignStatuses.Launched => "Campaign is now live",
            _ => "Continue campaign setup"
        };
    }

    public static CampaignWorkflowSummaryResponse BuildClientWorkflow(Campaign campaign)
    {
        var recommendation = RecommendationSelectionPolicy.GetVisibleRecommendation(campaign);
        var isProspectiveCampaign = ProspectCampaignPolicy.IsProspectiveCampaign(campaign);
        var recommendationAwaitingDecision = IsRecommendationAwaitingDecision(campaign, recommendation);
        var recommendationApprovalCompleted = HasRecommendationApprovalCompleted(campaign, recommendation);
        var paymentAwaitingManualReview = IsPaymentAwaitingManualReview(campaign);
        var hasClearedPayment = HasClearedPayment(campaign);
        var paymentRequiredBeforeApproval = !paymentAwaitingManualReview && !hasClearedPayment && !recommendationApprovalCompleted;

        if (paymentAwaitingManualReview)
        {
            return BuildClientWorkflow(
                currentStateKey: "payment_under_review",
                statusLabel: "Pay Later under review",
                headline: "Your Pay Later application is under review",
                description: "Your Finance Partner application has already been submitted. You do not need to pay again or approve anything while this review is pending.",
                nextStep: "We will update this workspace once the review outcome is confirmed.",
                requiresClientAction: false,
                actionLabel: "View status",
                paymentState: PaymentStateManualReview,
                paymentAwaitingManualReview: true,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: false,
                recommendationAwaitingDecision: recommendationAwaitingDecision,
                recommendationApprovalCompleted: recommendationApprovalCompleted,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (isProspectiveCampaign && recommendationAwaitingDecision && !recommendationApprovalCompleted)
        {
            return BuildClientWorkflow(
                currentStateKey: "recommendation_ready",
                statusLabel: "Proposal ready",
                headline: "Your proposal is ready to review",
                description: "Review the prepared proposal options, choose the one you want to move forward with, or send it back with feedback.",
                nextStep: "Select a proposal to continue to checkout, or request changes if you want the plan refined first.",
                requiresClientAction: true,
                actionLabel: "Review proposal",
                paymentState: PaymentStateRequired,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: false,
                recommendationAwaitingDecision: true,
                recommendationApprovalCompleted: false,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (isProspectiveCampaign && campaign.Status == CampaignStatuses.AwaitingPurchase)
        {
            return BuildClientWorkflow(
                currentStateKey: "planning_in_progress",
                statusLabel: "Proposal in progress",
                headline: "We are preparing your proposal",
                description: "Advertified is shaping a proposal for your business and will bring it here once it is ready to review.",
                nextStep: "We will notify you as soon as the proposal is available.",
                requiresClientAction: false,
                actionLabel: "View status",
                paymentState: PaymentStateRequired,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: false,
                recommendationAwaitingDecision: false,
                recommendationApprovalCompleted: false,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (paymentRequiredBeforeApproval && !isProspectiveCampaign)
        {
            return BuildClientWorkflow(
                currentStateKey: "payment_required",
                statusLabel: "Payment required",
                headline: "Payment is still required",
                description: recommendationAwaitingDecision
                    ? "Your recommendation is ready, but payment still needs to be completed before approval can continue."
                    : "Complete payment to unlock the next step for this campaign.",
                nextStep: "Finish payment to continue into recommendation review.",
                requiresClientAction: true,
                actionLabel: "Complete payment",
                paymentState: PaymentStateRequired,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: true,
                hasClearedPayment: false,
                recommendationAwaitingDecision: recommendationAwaitingDecision,
                recommendationApprovalCompleted: recommendationApprovalCompleted,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status is CampaignStatuses.Paid or CampaignStatuses.BriefInProgress)
        {
            return BuildClientWorkflow(
                currentStateKey: "brief_in_progress",
                statusLabel: "Brief in progress",
                headline: "Your campaign brief is the next step",
                description: "Your package is paid and the campaign is ready for detail capture or review.",
                nextStep: "Open the campaign workspace to continue the brief.",
                requiresClientAction: true,
                actionLabel: "Open campaign workspace",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: recommendationAwaitingDecision,
                recommendationApprovalCompleted: recommendationApprovalCompleted,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status == CampaignStatuses.BriefSubmitted
            || (campaign.Status == CampaignStatuses.PlanningInProgress && !recommendationAwaitingDecision))
        {
            return BuildClientWorkflow(
                currentStateKey: "planning_in_progress",
                statusLabel: "Planning in progress",
                headline: "We are preparing your recommendation",
                description: "Advertified is reviewing your brief and shaping the best route forward.",
                nextStep: "We will bring the recommendation here once it is ready.",
                requiresClientAction: false,
                actionLabel: "View campaign status",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: recommendationAwaitingDecision,
                recommendationApprovalCompleted: recommendationApprovalCompleted,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if ((campaign.Status == CampaignStatuses.ReviewReady || recommendationAwaitingDecision) && !recommendationApprovalCompleted)
        {
            return BuildClientWorkflow(
                currentStateKey: "recommendation_ready",
                statusLabel: "Needs approval",
                headline: "Your recommendation is ready to review",
                description: "Review the recommended media plan and approve it so Advertified can continue.",
                nextStep: "Approve the recommendation or send it back with notes.",
                requiresClientAction: true,
                actionLabel: "Review recommendation",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: recommendationAwaitingDecision,
                recommendationApprovalCompleted: false,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status == CampaignStatuses.CreativeSentToClientForApproval)
        {
            return BuildClientWorkflow(
                currentStateKey: "creative_review",
                statusLabel: "Content approval needed",
                headline: "Approve your campaign content",
                description: "Your campaign content is ready. Approve it so booking can begin, or send it back with revision notes.",
                nextStep: "Approve the content or request changes.",
                requiresClientAction: true,
                actionLabel: "Approve content",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: false,
                recommendationApprovalCompleted: true,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status == CampaignStatuses.CreativeChangesRequested)
        {
            return BuildClientWorkflow(
                currentStateKey: "creative_revision",
                statusLabel: "Revision in progress",
                headline: "Creative revisions are in progress",
                description: "Your feedback has been sent back to the team and the revised creative handoff is being prepared.",
                nextStep: "We will return the next approval here once the revision is ready.",
                requiresClientAction: false,
                actionLabel: "View campaign status",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: false,
                recommendationApprovalCompleted: true,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status == CampaignStatuses.CreativeApproved)
        {
            return BuildClientWorkflow(
                currentStateKey: "creative_approved",
                statusLabel: "Approved for booking",
                headline: "Creative approval is complete",
                description: "Your final content is approved and our team is moving into booking and launch preparation.",
                nextStep: "There is nothing you need to do right now.",
                requiresClientAction: false,
                actionLabel: "View campaign progress",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: false,
                recommendationApprovalCompleted: true,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status == CampaignStatuses.BookingInProgress)
        {
            return BuildClientWorkflow(
                currentStateKey: "booking_in_progress",
                statusLabel: "Booking in progress",
                headline: "We are booking your campaign now",
                description: "Placements, live dates, and supplier readiness are being confirmed before launch.",
                nextStep: "There is nothing you need to do right now.",
                requiresClientAction: false,
                actionLabel: "View campaign progress",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: false,
                recommendationApprovalCompleted: true,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        if (campaign.Status == CampaignStatuses.Launched)
        {
            return BuildClientWorkflow(
                currentStateKey: "live",
                statusLabel: "Campaign live",
                headline: "Your campaign is now live",
                description: "Operations has activated the campaign and it is now running.",
                nextStep: "Use this workspace for updates, reports, and support.",
                requiresClientAction: false,
                actionLabel: "Review live status",
                paymentState: PaymentStateCleared,
                paymentAwaitingManualReview: false,
                paymentRequiredBeforeApproval: false,
                hasClearedPayment: true,
                recommendationAwaitingDecision: false,
                recommendationApprovalCompleted: true,
                canOpenBrief: CanOpenBrief(campaign),
                canOpenPlanning: CanOpenPlanning(campaign));
        }

        return BuildClientWorkflow(
            currentStateKey: "recommendation_approved",
            statusLabel: "All set for now",
            headline: "Your campaign is moving forward",
            description: "Your recommendation has been approved and Advertified is handling the next production step.",
            nextStep: GetClientNextAction(campaign),
            requiresClientAction: false,
            actionLabel: "View campaign progress",
            paymentState: PaymentStateCleared,
            paymentAwaitingManualReview: false,
            paymentRequiredBeforeApproval: false,
            hasClearedPayment: true,
            recommendationAwaitingDecision: false,
            recommendationApprovalCompleted: true,
            canOpenBrief: CanOpenBrief(campaign),
            canOpenPlanning: CanOpenPlanning(campaign));
    }

    public static IReadOnlyList<CampaignTimelineStepResponse> BuildTimeline(Campaign campaign)
    {
        var latestRecommendation = RecommendationSelectionPolicy.GetVisibleRecommendation(campaign);
        var recommendationStatus = latestRecommendation?.Status?.Trim().ToLowerInvariant();
        var isProspectiveCampaign = ProspectCampaignPolicy.IsProspectiveCampaign(campaign);

        var paymentComplete = HasClearedPayment(campaign);
        var briefComplete = campaign.Status is not CampaignStatuses.Paid and not CampaignStatuses.BriefInProgress || campaign.CampaignBrief?.SubmittedAt is not null;
        var recommendationReady = campaign.Status is CampaignStatuses.PlanningInProgress or CampaignStatuses.ReviewReady or CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested or CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.BookingInProgress or CampaignStatuses.Launched || latestRecommendation is not null;
        var clientReviewActive = IsRecommendationAwaitingDecision(campaign, latestRecommendation);
        var recommendationApproved = campaign.Status is CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested or CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.BookingInProgress or CampaignStatuses.Launched || recommendationStatus == RecommendationStatuses.Approved;
        var creativeProductionStarted = campaign.Status is CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested or CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.BookingInProgress or CampaignStatuses.Launched;
        var creativeReviewActive = campaign.Status == CampaignStatuses.CreativeSentToClientForApproval;
        var creativeApproved = campaign.Status is CampaignStatuses.CreativeApproved or CampaignStatuses.BookingInProgress or CampaignStatuses.Launched;
        var bookingInProgress = campaign.Status == CampaignStatuses.BookingInProgress;
        var launchActivated = campaign.Status == CampaignStatuses.Launched;

        if (isProspectiveCampaign)
        {
            return new[]
            {
                BuildTimelineStep(
                    key: "proposal",
                    label: "Proposal prepared",
                    description: "Advertified has prepared proposal options tailored to your business.",
                    isComplete: recommendationReady,
                    isCurrent: campaign.Status == CampaignStatuses.AwaitingPurchase && latestRecommendation is null),
                BuildTimelineStep(
                    key: "review",
                    label: "Client review",
                    description: "Review the proposal, request changes, or choose the option you want to purchase.",
                    isComplete: recommendationApproved || paymentComplete,
                    isCurrent: clientReviewActive && !recommendationApproved && !paymentComplete),
                BuildTimelineStep(
                    key: "payment",
                    label: "Payment confirmed",
                    description: "Your selected proposal has been purchased and the campaign can move into production.",
                    isComplete: paymentComplete,
                    isCurrent: !paymentComplete && recommendationApproved),
                BuildTimelineStep(
                    key: "creative-production",
                    label: "Creative production",
                    description: "Advertified's creative director is preparing your finished media from the approved recommendation.",
                    isComplete: campaign.Status is CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.BookingInProgress or CampaignStatuses.Launched,
                    isCurrent: creativeProductionStarted && !creativeReviewActive && !creativeApproved),
                BuildTimelineStep(
                    key: "creative-approval",
                    label: "Final creative approval",
                    description: "Your finished media has been sent back for final client approval.",
                    isComplete: creativeApproved,
                    isCurrent: creativeReviewActive),
                BuildTimelineStep(
                    key: "booking",
                    label: "Supplier booking",
                    description: "Advertified is confirming placements and activation timing with suppliers.",
                    isComplete: bookingInProgress || launchActivated,
                    isCurrent: (creativeApproved && !bookingInProgress && !launchActivated) || bookingInProgress),
                BuildTimelineStep(
                    key: "live",
                    label: "Campaign live",
                    description: "Operations has activated the campaign and it is now live.",
                    isComplete: launchActivated,
                    isCurrent: false)
            };
        }

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
                isComplete: campaign.Status is CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeApproved or CampaignStatuses.BookingInProgress or CampaignStatuses.Launched,
                isCurrent: creativeProductionStarted && !creativeReviewActive && !creativeApproved),
            BuildTimelineStep(
                key: "creative-approval",
                label: "Final creative approval",
                description: "Your finished media has been sent back for final client approval.",
                isComplete: creativeApproved,
                isCurrent: creativeReviewActive),
            BuildTimelineStep(
                key: "booking",
                label: "Supplier booking",
                description: "Advertified is confirming placements and activation timing with suppliers.",
                isComplete: bookingInProgress || launchActivated,
                isCurrent: (creativeApproved && !bookingInProgress && !launchActivated) || bookingInProgress),
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
        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            return QueueStages.ClosedProspect;
        }

        var latestRecommendation = RecommendationSelectionPolicy.GetVisibleRecommendation(campaign);
        var hasRecommendation = latestRecommendation is not null;
        var recommendationStatus = latestRecommendation?.Status?.Trim().ToLowerInvariant();

        if (ProspectCampaignPolicy.IsProspectiveCampaign(campaign))
        {
            return recommendationStatus switch
            {
                RecommendationStatuses.SentToClient => QueueStages.WaitingOnClient,
                RecommendationStatuses.Approved => QueueStages.Completed,
                _ when hasRecommendation => QueueStages.AgentReview,
                _ => QueueStages.PlanningReady
            };
        }

        if (campaign.Status is CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched)
        {
            return QueueStages.Completed;
        }

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
            QueueStages.WaitingOnClient => "Waiting on client",
            QueueStages.ClosedProspect => "Closed prospect",
            QueueStages.Completed => "Completed",
            _ => "Watching"
        };
    }

    public static string GetAgentNextAction(Campaign campaign, string stage, Guid currentUserId)
    {
        if (string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase))
        {
            return "Final creative approval is captured. Start supplier booking and launch planning next.";
        }

        if (string.Equals(campaign.Status, CampaignStatuses.BookingInProgress, StringComparison.OrdinalIgnoreCase))
        {
            return "Supplier booking is in progress. Confirm live dates, update the client, and mark the campaign live when activation starts.";
        }

        if (string.Equals(campaign.Status, CampaignStatuses.Launched, StringComparison.OrdinalIgnoreCase))
        {
            return "Campaign is live. Monitor delivery and support any execution follow-up.";
        }

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            return "Prospect is closed. Reopen only if the client re-engages or commercial circumstances change.";
        }

        var latestRecommendation = RecommendationSelectionPolicy.GetVisibleRecommendation(campaign);
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

        if (ProspectCampaignPolicy.IsProspectiveCampaign(campaign))
        {
            return stage switch
            {
                QueueStages.PlanningReady => $"{assignmentPrefix} create the prospect proposal and prepare it for review.",
                QueueStages.AgentReview => $"{assignmentPrefix} review the proposal draft and send it to the prospect.",
                QueueStages.WaitingOnClient => $"{assignmentPrefix} wait for proposal feedback, selection, or purchase.",
                QueueStages.Completed => $"{assignmentPrefix} support conversion and activation follow-up.",
                _ => $"{assignmentPrefix} monitor this prospect and keep the opportunity moving."
            };
        }

        return stage switch
        {
            QueueStages.NewlyPaid => $"{assignmentPrefix} check the order and wait for the client brief.",
            QueueStages.BriefWaiting => $"{assignmentPrefix} monitor the brief and step in if the client needs help.",
            QueueStages.PlanningReady => $"{assignmentPrefix} open the campaign and create the recommendation.",
            QueueStages.AgentReview => $"{assignmentPrefix} review the AI draft, adjust the plan, and approve it before sending.",
            QueueStages.WaitingOnClient => $"{assignmentPrefix} wait for client approval or update requests.",
            QueueStages.ClosedProspect => $"{assignmentPrefix} keep this prospect closed unless the client re-engages.",
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
            QueueStages.BriefWaiting => 3,
            QueueStages.WaitingOnClient => 4,
            QueueStages.ClosedProspect => 5,
            QueueStages.Completed => 6,
            _ => 7
        };
    }

    public static bool CanOpenBrief(Campaign campaign)
    {
        return CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder)
            && (campaign.Status is CampaignStatuses.Paid
                or CampaignStatuses.BriefInProgress
                or CampaignStatuses.BriefSubmitted
                or CampaignStatuses.PlanningInProgress
                or CampaignStatuses.ReviewReady
                or CampaignStatuses.Approved
                or CampaignStatuses.CreativeSentToClientForApproval
                or CampaignStatuses.CreativeChangesRequested
                or CampaignStatuses.CreativeApproved
                or CampaignStatuses.BookingInProgress
                or CampaignStatuses.Launched);
    }

    public static bool CanOpenPlanning(Campaign campaign)
    {
        return CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder)
            && campaign.AiUnlocked
            && (campaign.Status is CampaignStatuses.BriefSubmitted
                or CampaignStatuses.PlanningInProgress
                or CampaignStatuses.ReviewReady
                or CampaignStatuses.Approved
                or CampaignStatuses.CreativeSentToClientForApproval
                or CampaignStatuses.CreativeChangesRequested
                or CampaignStatuses.CreativeApproved
                or CampaignStatuses.BookingInProgress
                or CampaignStatuses.Launched);
    }

    public static bool HasClearedPayment(Campaign campaign)
    {
        return string.Equals(campaign.PackageOrder?.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
            || campaign.Status is CampaignStatuses.Approved
                or CampaignStatuses.CreativeSentToClientForApproval
                or CampaignStatuses.CreativeChangesRequested
                or CampaignStatuses.CreativeApproved
                or CampaignStatuses.BookingInProgress
                or CampaignStatuses.Launched;
    }

    public static bool HasRecommendationApprovalCompleted(Campaign campaign, CampaignRecommendation? recommendation)
    {
        return string.Equals(recommendation?.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            || campaign.Status is CampaignStatuses.Approved
                or CampaignStatuses.CreativeSentToClientForApproval
                or CampaignStatuses.CreativeChangesRequested
                or CampaignStatuses.CreativeApproved
                or CampaignStatuses.BookingInProgress
                or CampaignStatuses.Launched;
    }

    public static bool IsPaymentAwaitingManualReview(Campaign campaign)
    {
        return string.Equals(campaign.PackageOrder?.PaymentProvider, "lula", StringComparison.OrdinalIgnoreCase)
            && string.Equals(campaign.PackageOrder?.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsRecommendationAwaitingDecision(Campaign campaign, CampaignRecommendation? recommendation)
    {
        return string.Equals(campaign.Status, CampaignStatuses.ReviewReady, StringComparison.OrdinalIgnoreCase)
            || (ProspectCampaignPolicy.IsProspectiveCampaign(campaign)
                && string.Equals(recommendation?.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase));
    }

    private static CampaignWorkflowSummaryResponse BuildClientWorkflow(
        string currentStateKey,
        string statusLabel,
        string headline,
        string description,
        string nextStep,
        bool requiresClientAction,
        string actionLabel,
        string paymentState,
        bool paymentAwaitingManualReview,
        bool paymentRequiredBeforeApproval,
        bool hasClearedPayment,
        bool recommendationAwaitingDecision,
        bool recommendationApprovalCompleted,
        bool canOpenBrief,
        bool canOpenPlanning)
    {
        return new CampaignWorkflowSummaryResponse
        {
            CurrentStateKey = currentStateKey,
            StatusLabel = statusLabel,
            Headline = headline,
            Description = description,
            NextStep = nextStep,
            RequiresClientAction = requiresClientAction,
            ActionLabel = actionLabel,
            PaymentState = paymentState,
            PaymentAwaitingManualReview = paymentAwaitingManualReview,
            PaymentRequiredBeforeApproval = paymentRequiredBeforeApproval,
            HasClearedPayment = hasClearedPayment,
            RecommendationAwaitingDecision = recommendationAwaitingDecision,
            RecommendationApprovalCompleted = recommendationApprovalCompleted,
            CanOpenBrief = canOpenBrief,
            CanOpenPlanning = canOpenPlanning
        };
    }
}

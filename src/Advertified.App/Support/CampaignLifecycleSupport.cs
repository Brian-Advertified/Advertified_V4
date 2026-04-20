using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Data.Entities;
using System.Linq;

namespace Advertified.App.Support;

internal static class CampaignLifecycleSupport
{
    public static CampaignLifecycleResponse Build(Campaign campaign)
    {
        var visibleRecommendation = RecommendationSelectionPolicy.GetVisibleRecommendation(campaign);
        var visibleRecommendationSet = RecommendationSelectionPolicy.GetVisibleRecommendationSet(campaign);
        var proposalState = ResolveProposalState(campaign, visibleRecommendation, visibleRecommendationSet);
        var paymentState = ResolvePaymentState(campaign);
        var commercialState = ResolveCommercialState(campaign, proposalState, paymentState, visibleRecommendationSet.Length > 0);
        var communicationState = ResolveCommunicationState(campaign, visibleRecommendation);
        var fulfilmentState = string.IsNullOrWhiteSpace(campaign.Status) ? "unknown" : campaign.Status.Trim().ToLowerInvariant();
        var currentState = ResolveCurrentState(campaign, proposalState, paymentState, fulfilmentState);
        var aiStudioAccessState = ResolveAiStudioAccessState(campaign);

        return new CampaignLifecycleResponse
        {
            CurrentState = currentState,
            ProposalState = proposalState,
            PaymentState = paymentState,
            CommercialState = commercialState,
            CommunicationState = communicationState,
            FulfilmentState = fulfilmentState,
            AiStudioAccessState = aiStudioAccessState
        };
    }

    private static string ResolveProposalState(Campaign campaign, CampaignRecommendation? visibleRecommendation, IReadOnlyList<CampaignRecommendation> visibleRecommendationSet)
    {
        if (CampaignWorkflowPolicy.HasRecommendationApprovalCompleted(campaign, visibleRecommendation))
        {
            return "approved";
        }

        var recommendationDeliveries = campaign.EmailDeliveryMessages
            .Where(message =>
                string.Equals(message.DeliveryPurpose, "recommendation_ready", StringComparison.OrdinalIgnoreCase)
                && (visibleRecommendation is null || message.RecommendationRevisionNumber == visibleRecommendation.RevisionNumber))
            .ToArray();

        if (recommendationDeliveries.Any(message => message.OpenedAt.HasValue || message.ClickedAt.HasValue))
        {
            return "opened";
        }

        if (recommendationDeliveries.Any(message => message.DeliveredAt.HasValue))
        {
            return "delivered";
        }

        if (recommendationDeliveries.Any(message =>
                string.Equals(message.Status, EmailDeliveryStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.Status, EmailDeliveryStatuses.Delivered, StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.Status, EmailDeliveryStatuses.DeliveryDelayed, StringComparison.OrdinalIgnoreCase))
            || string.Equals(visibleRecommendation?.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase)
            || string.Equals(campaign.Status, CampaignStatuses.ReviewReady, StringComparison.OrdinalIgnoreCase))
        {
            return "sent";
        }

        if (visibleRecommendationSet.Count > 0)
        {
            return "ready_to_send";
        }

        return "draft";
    }

    private static string ResolvePaymentState(Campaign campaign)
    {
        var order = campaign.PackageOrder;
        if (order is null)
        {
            return "not_started";
        }

        if (string.Equals(order.RefundStatus, "refunded", StringComparison.OrdinalIgnoreCase))
        {
            return "refunded";
        }

        if (string.Equals(order.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return "paid";
        }

        if (CampaignWorkflowPolicy.IsPaymentAwaitingManualReview(campaign))
        {
            return "under_review";
        }

        if (string.Equals(order.PaymentStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "failed";
        }

        if (PackageOrderIntentPolicy.IsProspect(order))
        {
            return "not_started";
        }

        return string.Equals(order.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase)
            ? "payment_pending"
            : "not_started";
    }

    private static string ResolveCommercialState(Campaign campaign, string proposalState, string paymentState, bool hasRecommendation)
    {
        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            return "closed_lost";
        }

        var isProspectIntent = PackageOrderIntentPolicy.IsProspect(campaign.PackageOrder);
        if (!isProspectIntent)
        {
            if (paymentState == "paid")
            {
                return "converted";
            }

            return proposalState is "opened" or "delivered"
                ? "negotiating"
                : proposalState == "sent"
                    ? "proposal_sent"
                    : "prospect";
        }

        if (paymentState == "paid")
        {
            return "converted";
        }

        return proposalState switch
        {
            "opened" => "negotiating",
            "delivered" or "sent" => "proposal_sent",
            "ready_to_send" when hasRecommendation => "prospect",
            "draft" when hasRecommendation => "prospect",
            _ => "prospect"
        };
    }

    private static string ResolveCommunicationState(Campaign campaign, CampaignRecommendation? visibleRecommendation)
    {
        var recommendationDeliveries = campaign.EmailDeliveryMessages
            .Where(message =>
                string.Equals(message.DeliveryPurpose, "recommendation_ready", StringComparison.OrdinalIgnoreCase)
                && (visibleRecommendation is null || message.RecommendationRevisionNumber == visibleRecommendation.RevisionNumber))
            .OrderByDescending(message => message.CreatedAt)
            .ToArray();

        if (recommendationDeliveries.Any(message => message.OpenedAt.HasValue || message.ClickedAt.HasValue || message.DeliveredAt.HasValue))
        {
            return "delivered";
        }

        if (recommendationDeliveries.Any(message =>
                string.Equals(message.Status, EmailDeliveryStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.Status, EmailDeliveryStatuses.DeliveryDelayed, StringComparison.OrdinalIgnoreCase)))
        {
            return "sending";
        }

        if (recommendationDeliveries.Any(message => string.Equals(message.Status, EmailDeliveryStatuses.Pending, StringComparison.OrdinalIgnoreCase)))
        {
            return "queued";
        }

        if (recommendationDeliveries.Any(message =>
                string.Equals(message.Status, EmailDeliveryStatuses.Failed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.Status, EmailDeliveryStatuses.Bounced, StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.Status, EmailDeliveryStatuses.Complained, StringComparison.OrdinalIgnoreCase)))
        {
            return "failed";
        }

        return string.Equals(visibleRecommendation?.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase)
            || string.Equals(campaign.Status, CampaignStatuses.ReviewReady, StringComparison.OrdinalIgnoreCase)
            ? "sent"
            : "queued";
    }

    private static string ResolveCurrentState(Campaign campaign, string proposalState, string paymentState, string fulfilmentState)
    {
        if (string.Equals(fulfilmentState, CampaignStatuses.Launched, StringComparison.OrdinalIgnoreCase))
        {
            return "launched";
        }

        var activationReady = paymentState == "paid"
            && (proposalState == "approved"
                || campaign.Status is CampaignStatuses.Approved
                    or CampaignStatuses.CreativeChangesRequested
                    or CampaignStatuses.CreativeSentToClientForApproval
                    or CampaignStatuses.CreativeApproved
                    or CampaignStatuses.BookingInProgress);
        if (activationReady)
        {
            return "activation_ready";
        }

        if (paymentState == "paid")
        {
            return "paid";
        }

        if (paymentState is "payment_pending" or "under_review")
        {
            return "payment_pending";
        }

        return proposalState;
    }

    private static string ResolveAiStudioAccessState(Campaign campaign)
    {
        if (!campaign.AiUnlocked)
        {
            return "locked";
        }

        return campaign.Status is CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched
            ? "creative_unlocked"
            : "brief_unlocked";
    }

    public static DateTime ResolveLastActivityAt(Campaign campaign)
    {
        var candidates = new List<DateTime>
        {
            campaign.UpdatedAt,
            campaign.CreatedAt
        };

        if (campaign.AssignedAt.HasValue)
        {
            candidates.Add(campaign.AssignedAt.Value);
        }

        foreach (var recommendation in campaign.CampaignRecommendations)
        {
            candidates.Add(recommendation.UpdatedAt);
            if (recommendation.SentToClientAt.HasValue)
            {
                candidates.Add(recommendation.SentToClientAt.Value);
            }
        }

        foreach (var delivery in campaign.EmailDeliveryMessages)
        {
            candidates.Add(delivery.UpdatedAt);
            if (delivery.LatestEventAt.HasValue)
            {
                candidates.Add(delivery.LatestEventAt.Value);
            }
        }

        return candidates.Max();
    }
}

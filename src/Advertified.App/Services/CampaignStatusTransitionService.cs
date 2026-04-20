using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class CampaignStatusTransitionService : ICampaignStatusTransitionService
{
    public void MoveRecommendationSetToReviewReady(Campaign campaign, IEnumerable<CampaignRecommendation> recommendations, DateTime now)
    {
        foreach (var recommendation in recommendations)
        {
            recommendation.Status = RecommendationStatuses.SentToClient;
            recommendation.SentToClientAt = now;
            recommendation.UpdatedAt = now;
        }

        campaign.Status = CampaignStatuses.ReviewReady;
        campaign.UpdatedAt = now;
    }

    public void MoveRecommendationToApproved(Campaign campaign, CampaignRecommendation recommendation, DateTime now)
    {
        recommendation.Status = RecommendationStatuses.Approved;
        recommendation.ApprovedAt = now;
        recommendation.UpdatedAt = now;
        campaign.Status = CampaignStatuses.Approved;
        campaign.UpdatedAt = now;
    }

    public void MoveRecommendationBackToPlanning(Campaign campaign, DateTime now)
    {
        campaign.Status = CampaignStatuses.PlanningInProgress;
        campaign.UpdatedAt = now;
    }

    public void MoveCreativeToClientApproval(Campaign campaign, DateTime now)
    {
        if (!string.Equals(campaign.Status, CampaignStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(campaign.Status, CampaignStatuses.CreativeChangesRequested, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Finished media can only be sent to the client after the recommendation has been approved and while creative production or creative revision is active.");
        }

        campaign.Status = CampaignStatuses.CreativeSentToClientForApproval;
        campaign.UpdatedAt = now;
    }

    public void MoveCreativeBackForChanges(Campaign campaign, DateTime now)
    {
        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeSentToClientForApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Creative changes can only be requested after finished media has been sent back for client approval.");
        }

        campaign.Status = CampaignStatuses.CreativeChangesRequested;
        campaign.UpdatedAt = now;
    }

    public void MoveCreativeToApproved(Campaign campaign, DateTime now)
    {
        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeSentToClientForApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Finished media can only be approved once it has been sent back to the client for final review.");
        }

        campaign.Status = CampaignStatuses.CreativeApproved;
        campaign.UpdatedAt = now;
    }

    public bool TryMoveToBookingInProgress(Campaign campaign, DateTime now)
    {
        if (string.Equals(campaign.Status, CampaignStatuses.BookingInProgress, StringComparison.OrdinalIgnoreCase))
        {
            campaign.UpdatedAt = now;
            return false;
        }

        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        campaign.Status = CampaignStatuses.BookingInProgress;
        campaign.UpdatedAt = now;
        return true;
    }

    public void MoveCampaignToLaunched(Campaign campaign, DateTime now)
    {
        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(campaign.Status, CampaignStatuses.BookingInProgress, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only campaigns with final creative approval captured or supplier booking underway can be activated as live.");
        }

        campaign.Status = CampaignStatuses.Launched;
        campaign.UpdatedAt = now;
    }
}

using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignStatusTransitionService
{
    void MoveRecommendationSetToReviewReady(Campaign campaign, IEnumerable<CampaignRecommendation> recommendations, DateTime now);

    void MoveRecommendationToApproved(Campaign campaign, CampaignRecommendation recommendation, DateTime now);

    void MoveRecommendationBackToPlanning(Campaign campaign, DateTime now);

    void MoveCreativeToClientApproval(Campaign campaign, DateTime now);

    void MoveCreativeBackForChanges(Campaign campaign, DateTime now);

    void MoveCreativeToApproved(Campaign campaign, DateTime now);

    bool TryMoveToBookingInProgress(Campaign campaign, DateTime now);

    void MoveCampaignToLaunched(Campaign campaign, DateTime now);
}

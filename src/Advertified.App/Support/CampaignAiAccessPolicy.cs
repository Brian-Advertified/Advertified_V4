using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

internal static class CampaignAiAccessPolicy
{
    public static bool ShouldUnlock(Campaign campaign, CampaignBrief? brief = null)
    {
        var effectiveBrief = brief ?? campaign.CampaignBrief;
        if (effectiveBrief?.SubmittedAt is not null)
        {
            return true;
        }

        return campaign.Status is CampaignStatuses.BriefSubmitted
            or CampaignStatuses.PlanningInProgress
            or CampaignStatuses.ReviewReady
            or CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched;
    }

    public static void Apply(Campaign campaign, CampaignBrief? brief = null)
    {
        campaign.AiUnlocked = ShouldUnlock(campaign, brief);
    }
}

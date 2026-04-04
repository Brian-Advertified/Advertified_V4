using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class CampaignStatusTransitionPolicy
{
    public static bool TryAdvanceToBookingInProgress(Campaign campaign)
    {
        if (string.Equals(campaign.Status, CampaignStatuses.BookingInProgress, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(campaign.Status, CampaignStatuses.CreativeSentToClientForApproval, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        campaign.Status = CampaignStatuses.BookingInProgress;
        return true;
    }
}

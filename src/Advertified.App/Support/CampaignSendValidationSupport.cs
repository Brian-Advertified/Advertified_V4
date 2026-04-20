using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Data.Entities;
using System.Net.Mail;
using System.Linq;

namespace Advertified.App.Support;

internal static class CampaignSendValidationSupport
{
    public static CampaignSendValidationResponse Build(Campaign campaign)
    {
        var recommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        return Build(campaign, recommendations);
    }

    public static CampaignSendValidationResponse Build(Campaign campaign, IReadOnlyList<CampaignRecommendation> recommendations)
    {
        var reasons = new List<string>();

        if (recommendations.Count == 0)
        {
            reasons.Add("Recommendation not found.");
            return new CampaignSendValidationResponse
            {
                CanSendRecommendation = false,
                Reasons = reasons
            };
        }

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            reasons.Add("Reopen this prospect before sending a recommendation to the client.");
        }

        if (campaign.Status is CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched
            || recommendations.Any(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add("This campaign is already approved and can no longer be sent from the recommendation stage.");
        }

        if (recommendations.Count < 3)
        {
            reasons.Add("Three proposal options are required before sending.");
        }

        var clientEmail = campaign.ResolveClientEmail();
        if (string.IsNullOrWhiteSpace(clientEmail) || !IsValidEmailAddress(clientEmail))
        {
            reasons.Add("A valid client or prospect email address is required before sending.");
        }

        if (string.IsNullOrWhiteSpace(campaign.ResolveClientName()))
        {
            reasons.Add("A client or prospect contact name is required before sending.");
        }

        for (var index = 0; index < recommendations.Count; index++)
        {
            if (!RecommendationOohPolicy.ContainsOoh(recommendations[index].RecommendationItems.Select(item => item.InventoryType)))
            {
                reasons.Add($"Proposal {index + 1} is missing Billboards and Digital Screens.");
            }
        }

        return new CampaignSendValidationResponse
        {
            CanSendRecommendation = reasons.Count == 0,
            Reasons = reasons
        };
    }

    private static bool IsValidEmailAddress(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

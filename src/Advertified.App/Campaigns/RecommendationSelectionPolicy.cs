using Advertified.App.Data.Entities;
using Advertified.App.Support;

namespace Advertified.App.Campaigns;

internal static class RecommendationSelectionPolicy
{
    internal static CampaignRecommendation[] GetVisibleRecommendationSet(Campaign campaign)
        => GetVisibleRecommendationSet(campaign.Status, campaign.CampaignRecommendations);

    internal static CampaignRecommendation[] GetVisibleRecommendationSet(string? campaignStatus, IEnumerable<CampaignRecommendation> recommendations)
    {
        var materialized = recommendations.ToArray();
        if (materialized.Length == 0)
        {
            return Array.Empty<CampaignRecommendation>();
        }

        var approvedRecommendation = materialized
            .Where(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ApprovedAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.RevisionNumber)
            .FirstOrDefault();
        if (approvedRecommendation is not null)
        {
            return new[] { approvedRecommendation };
        }

        if (string.Equals(campaignStatus, CampaignStatuses.ReviewReady, StringComparison.OrdinalIgnoreCase))
        {
            var sentRevisionNumber = materialized
                .Where(x => string.Equals(x.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.SentToClientAt ?? x.UpdatedAt)
                .ThenByDescending(x => x.RevisionNumber)
                .Select(x => (int?)x.RevisionNumber)
                .FirstOrDefault();
            if (sentRevisionNumber.HasValue)
            {
                return RecommendationRevisionSupport.GetRevisionSet(materialized, sentRevisionNumber.Value);
            }
        }

        return RecommendationRevisionSupport.GetCurrentRecommendationSet(materialized);
    }

    internal static CampaignRecommendation? GetVisibleRecommendation(Campaign campaign)
        => GetVisibleRecommendation(campaign.Status, campaign.CampaignRecommendations);

    internal static CampaignRecommendation? GetVisibleRecommendation(string? campaignStatus, IEnumerable<CampaignRecommendation> recommendations)
    {
        var currentSet = GetVisibleRecommendationSet(campaignStatus, recommendations);
        return currentSet.FirstOrDefault(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            ?? currentSet.FirstOrDefault(x => string.Equals(x.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase))
            ?? currentSet
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
    }
}

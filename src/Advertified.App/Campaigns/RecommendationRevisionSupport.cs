using Advertified.App.Data.Entities;

namespace Advertified.App.Campaigns;

internal static class RecommendationRevisionSupport
{
    internal static int GetNextRevisionNumber(IEnumerable<CampaignRecommendation> recommendations)
        => recommendations.Any() ? recommendations.Max(x => x.RevisionNumber) + 1 : 1;

    internal static CampaignRecommendation[] GetCurrentRecommendationSet(IEnumerable<CampaignRecommendation> recommendations)
    {
        var materialized = recommendations.ToArray();
        if (materialized.Length == 0)
        {
            return Array.Empty<CampaignRecommendation>();
        }

        var revisionNumber = materialized.Max(x => x.RevisionNumber);
        return GetRevisionSet(materialized, revisionNumber);
    }

    internal static CampaignRecommendation[] GetRevisionSet(IEnumerable<CampaignRecommendation> recommendations, int revisionNumber)
    {
        return recommendations
            .Where(x => x.RevisionNumber == revisionNumber)
            .OrderBy(x => GetProposalRank(x.RecommendationType))
            .ThenBy(x => x.CreatedAt)
            .ToArray();
    }

    internal static List<CampaignRecommendation> CloneAsDraftRevision(IEnumerable<CampaignRecommendation> recommendations, int nextRevisionNumber, DateTime now, string? clientFeedbackNotes)
    {
        return recommendations
            .Select(source =>
            {
                var clone = new CampaignRecommendation
                {
                    Id = Guid.NewGuid(),
                    CampaignId = source.CampaignId,
                    RecommendationType = source.RecommendationType,
                    GeneratedBy = source.GeneratedBy,
                    Status = "draft",
                    TotalCost = source.TotalCost,
                    Summary = source.Summary,
                    Rationale = MergeClientFeedback(source.Rationale, clientFeedbackNotes),
                    CreatedByUserId = source.CreatedByUserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RevisionNumber = nextRevisionNumber,
                    SentToClientAt = null,
                    ApprovedAt = null
                };

                foreach (var item in source.RecommendationItems.OrderBy(x => x.CreatedAt))
                {
                    clone.RecommendationItems.Add(new RecommendationItem
                    {
                        Id = Guid.NewGuid(),
                        RecommendationId = clone.Id,
                        InventoryType = item.InventoryType,
                        InventoryItemId = item.InventoryItemId,
                        DisplayName = item.DisplayName,
                        Quantity = item.Quantity,
                        UnitCost = item.UnitCost,
                        TotalCost = item.TotalCost,
                        MetadataJson = item.MetadataJson,
                        CreatedAt = now
                    });
                }

                return clone;
            })
            .ToList();
    }

    private static string MergeClientFeedback(string? rationale, string? clientFeedbackNotes)
    {
        var baseText = rationale ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientFeedbackNotes))
        {
            return baseText;
        }

        return $"{baseText.Trim()}\n\nClient feedback: {clientFeedbackNotes.Trim()}".Trim();
    }

    private static int GetProposalRank(string? recommendationType)
    {
        var variantKey = recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => 0,
            "ooh_focus" => 1,
            "radio_focus" => 2,
            "digital_focus" => 2,
            _ => 9
        };
    }
}

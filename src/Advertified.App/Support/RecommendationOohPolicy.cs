using Advertified.App.Contracts.Agent;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Support;

internal static class RecommendationOohPolicy
{
    private const string MissingOohMessage = "Billboards and Digital Screens are required in every recommendation.";

    public static void EnsureGeneratedPlanContainsOoh(IEnumerable<PlannedItem> items)
    {
        if (!ContainsOoh(items.Select(item => item.MediaType)))
        {
            throw new ArgumentException(MissingOohMessage);
        }
    }

    public static void EnsureSelectedInventoryContainsOoh(IReadOnlyList<SelectedInventoryItemRequest> items)
    {
        if (!ContainsOoh(items.Select(item => item.Type)))
        {
            throw new ArgumentException(MissingOohMessage);
        }
    }

    public static void EnsureRecommendationContainsOoh(IEnumerable<RecommendationItem> items)
    {
        if (!ContainsOoh(items.Select(item => item.InventoryType)))
        {
            throw new ArgumentException(MissingOohMessage);
        }
    }

    public static bool ContainsOoh(IEnumerable<string?> channels)
    {
        return channels.Any(channel => PlanningChannelSupport.IsOohFamilyChannel(channel));
    }
}

using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class ProspectDispositionStatuses
{
    public const string Open = "open";
    public const string Closed = "closed";
}

public static class ProspectCampaignPolicy
{
    public static bool IsProspectiveCampaign(Campaign campaign)
    {
        return string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase)
            || (campaign.PackageOrder?.PaymentProvider == "prospect"
                && !string.Equals(campaign.PackageOrder?.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsClosed(Campaign campaign)
    {
        return string.Equals(campaign.ProspectDispositionStatus, ProspectDispositionStatuses.Closed, StringComparison.OrdinalIgnoreCase);
    }
}

using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

internal static class ProspectContactSupport
{
    public static string ResolveClientName(this Campaign campaign)
    {
        return !string.IsNullOrWhiteSpace(campaign.User?.FullName)
            ? campaign.User.FullName
            : campaign.ProspectLead?.FullName ?? string.Empty;
    }

    public static string ResolveClientEmail(this Campaign campaign)
    {
        return !string.IsNullOrWhiteSpace(campaign.User?.Email)
            ? campaign.User.Email
            : campaign.ProspectLead?.Email ?? string.Empty;
    }

    public static string? ResolveBusinessName(this Campaign campaign)
    {
        return campaign.User?.BusinessProfile?.BusinessName;
    }

    public static string? ResolveIndustry(this Campaign campaign)
    {
        return campaign.User?.BusinessProfile?.Industry;
    }

    public static string? ResolveClientPhone(this Campaign campaign)
    {
        return !string.IsNullOrWhiteSpace(campaign.User?.Phone)
            ? campaign.User.Phone
            : campaign.ProspectLead?.Phone;
    }

    public static string ResolveClientName(this PackageOrder order)
    {
        return !string.IsNullOrWhiteSpace(order.User?.FullName)
            ? order.User.FullName
            : order.ProspectLead?.FullName ?? string.Empty;
    }

    public static string ResolveClientEmail(this PackageOrder order)
    {
        return !string.IsNullOrWhiteSpace(order.User?.Email)
            ? order.User.Email
            : order.ProspectLead?.Email ?? string.Empty;
    }

    public static string ResolveClientPhone(this PackageOrder order)
    {
        return !string.IsNullOrWhiteSpace(order.User?.Phone)
            ? order.User.Phone
            : order.ProspectLead?.Phone ?? string.Empty;
    }
}

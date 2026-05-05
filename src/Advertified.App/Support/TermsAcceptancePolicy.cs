using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class TermsAcceptancePolicy
{
    public const string CurrentVersion = "2026-04-05";

    public static void Capture(PackageOrder order, string source, DateTime now)
    {
        if (order.TermsAcceptedAt.HasValue)
        {
            return;
        }

        order.TermsAcceptedAt = now;
        order.TermsVersion = CurrentVersion;
        order.TermsAcceptanceSource = string.IsNullOrWhiteSpace(source) ? "system" : source.Trim().ToLowerInvariant();
    }
}

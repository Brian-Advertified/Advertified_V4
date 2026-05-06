using System.Globalization;

namespace Advertified.App.Support;

internal static class CurrencyFormatSupport
{
    private static readonly CultureInfo SouthAfricanCulture = CultureInfo.GetCultureInfo("en-ZA");
    private static readonly CultureInfo StandardSeparatorsCulture = CultureInfo.InvariantCulture;

    internal static string FormatZar(decimal amount)
    {
        return $"R {amount.ToString("N2", SouthAfricanCulture)}";
    }

    internal static string FormatZarStandard(decimal amount)
    {
        return $"R {amount.ToString("N2", StandardSeparatorsCulture)}";
    }

    internal static string FormatZarWhole(decimal amount)
    {
        return amount.ToString("C0", SouthAfricanCulture);
    }
}

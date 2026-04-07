using System.Globalization;

namespace Advertified.App.Services.BroadcastMatching;

internal static class BroadcastMatchingHelpers
{
    public static string NormalizeToken(string value)
    {
        var normalized = value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
        return normalized switch
        {
            "isizulu" => "zulu",
            "isixhosa" => "xhosa",
            "sesotho" => "sotho",
            "setswana" => "tswana",
            "sepedi" => "pedi",
            "siswati" => "swati",
            "isitshivenda" => "venda",
            "tshivenda" => "venda",
            "isindebele" => "ndebele",
            "itsonga" => "xitsonga",
            _ => normalized
        };
    }

    public static List<string> NormalizeTokens(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeToken)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> NormalizeLabels(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static int OverlapCount(IEnumerable<string> left, IEnumerable<string> right)
    {
        var normalizedLeft = NormalizeTokens(left);
        var normalizedRight = NormalizeTokens(right);
        return normalizedLeft.Intersect(normalizedRight, StringComparer.OrdinalIgnoreCase).Count();
    }

    public static bool HasOverlap(IEnumerable<string> left, IEnumerable<string> right)
    {
        var normalizedLeft = NormalizeTokens(left);
        var normalizedRight = NormalizeTokens(right);
        return normalizedLeft.Intersect(normalizedRight, StringComparer.OrdinalIgnoreCase).Any();
    }

    public static decimal Ratio(int matched, int total) =>
        total <= 0 ? 0m : decimal.Divide(matched, total);

    public static decimal ClampScore(decimal score) => Math.Max(0m, score);

    public static bool EqualsText(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(NormalizeToken(left), NormalizeToken(right), StringComparison.OrdinalIgnoreCase);

    public static bool RangeOverlaps(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var normalizedLeft = left.Trim().ToLowerInvariant();
        var normalizedRight = right.Trim().ToLowerInvariant();
        return normalizedLeft.Contains(normalizedRight, StringComparison.OrdinalIgnoreCase)
            || normalizedRight.Contains(normalizedLeft, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsAnyText(string? source, IEnumerable<string> terms)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var normalizedSource = NormalizeToken(source);
        return terms.Any(term => normalizedSource.Contains(NormalizeToken(term), StringComparison.OrdinalIgnoreCase));
    }

    public static decimal PercentileRank(long value, IReadOnlyList<long> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0m;
        }

        if (sortedValues.Count == 1)
        {
            return 1m;
        }

        var lessOrEqual = sortedValues.Count(item => item <= value);
        return decimal.Divide(lessOrEqual - 1, sortedValues.Count - 1);
    }

    public static decimal? GetMinimumPrice(BroadcastMediaOutlet outlet)
    {
        var positive = outlet.PricePointsZar.Where(static price => price > 0m).ToList();
        return positive.Count > 0 ? positive.Min() : null;
    }

    public static string HealthFlag(BroadcastCatalogHealth health) => health switch
    {
        BroadcastCatalogHealth.Strong => "strong_catalog_health",
        BroadcastCatalogHealth.MixedNotFullyHealthy => "mixed_catalog_health",
        BroadcastCatalogHealth.WeakUnpriced => "weak_unpriced",
        BroadcastCatalogHealth.WeakNoInventory => "weak_no_inventory",
        BroadcastCatalogHealth.WeakPartialPricing => "weak_partial_pricing",
        _ => "unknown_catalog_health"
    };

    public static string FormatScore(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

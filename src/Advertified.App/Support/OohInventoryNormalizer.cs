using System.Globalization;
using System.Text.RegularExpressions;

namespace Advertified.App.Support;

public static partial class OohInventoryNormalizer
{
    private static readonly string[] PlacementKeywords =
    {
        "billboard",
        "screen",
        "banner",
        "wrap",
        "wall",
        "lift",
        "parking",
        "atrium",
        "elevator",
        "entrance",
        "exit",
        "escalator",
        "balustrade",
        "glass",
        "gantry",
        "pillar",
        "cube",
        "tower",
        "taxi",
        "roadside",
        "door",
        "panel",
        "panels",
        "hanging",
        "corner"
    };

    public static string NormalizeSubtype(
        string? rawSubtype,
        string? rawSlotType,
        string? displayName,
        string? city,
        string? suburb,
        string? province)
    {
        var normalizedSubtype = NormalizePlacementLabel(rawSubtype);
        if (IsUsefulPlacementLabel(normalizedSubtype, city, suburb, province))
        {
            return normalizedSubtype!;
        }

        var normalizedSlotType = NormalizePlacementLabel(rawSlotType);
        if (IsUsefulPlacementLabel(normalizedSlotType, city, suburb, province))
        {
            return normalizedSlotType!;
        }

        var inferredSubtype = NormalizePlacementLabel(displayName);
        if (IsUsefulPlacementLabel(inferredSubtype, city, suburb, province))
        {
            return inferredSubtype!;
        }

        return "Placement";
    }

    public static string NormalizeSlotType(string? rawSlotType, string? normalizedSubtype)
    {
        var normalizedSlotType = NormalizePlacementLabel(rawSlotType);
        if (!string.IsNullOrWhiteSpace(normalizedSlotType))
        {
            return normalizedSlotType;
        }

        return string.IsNullOrWhiteSpace(normalizedSubtype)
            ? "Placement"
            : normalizedSubtype;
    }

    private static bool IsUsefulPlacementLabel(
        string? value,
        string? city,
        string? suburb,
        string? province)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Equals("Placement", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ContainsPlacementKeyword(normalized)
               || !LooksLikeLocation(normalized, city, suburb, province);
    }

    private static bool ContainsPlacementKeyword(string value) =>
        PlacementKeywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeLocation(
        string value,
        string? city,
        string? suburb,
        string? province)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = CollapseWhitespace(value).Trim();
        if (ContainsPlacementKeyword(normalized))
        {
            return false;
        }

        var locationTokens = new[]
            {
                city,
                suburb,
                province
            }
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => NormalizeLocationToken(token!))
            .ToList();

        if (locationTokens.Contains(NormalizeLocationToken(normalized)))
        {
            return true;
        }

        return normalized.Contains(',')
               || normalized.Contains("mall", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("pretoria", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("johannesburg", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("cape town", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLocationToken(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        return NonAlphaNumericRegex().Replace(lowered, " ").Trim();
    }

    private static string? NormalizePlacementLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = CollapseWhitespace(value)
            .Replace(" ,", ",", StringComparison.Ordinal)
            .Trim(' ', ',', '|');

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        var lowered = cleaned.ToLowerInvariant();
        var titleCased = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lowered);
        return Regex.Replace(titleCased, @"\s*\|\s*", " | ");
    }

    private static string CollapseWhitespace(string value) =>
        MultiWhitespaceRegex().Replace(value, " ");

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiWhitespaceRegex();
}

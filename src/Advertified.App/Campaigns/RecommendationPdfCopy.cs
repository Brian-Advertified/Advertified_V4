using System.Text.RegularExpressions;
using Advertified.App.Support;

namespace Advertified.App.Campaigns;

internal static class RecommendationPdfCopy
{
    internal static string NormalizeRecommendationChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("ooh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("billboard", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("digital_screen", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("digitalscreen", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("digital screen", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("digital screens", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("out of home", StringComparison.OrdinalIgnoreCase))
        {
            return "ooh";
        }

        if (normalized.Contains("radio", StringComparison.OrdinalIgnoreCase))
        {
            return "radio";
        }

        if (normalized.Contains("tv", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("television", StringComparison.OrdinalIgnoreCase))
        {
            return "tv";
        }

        if (normalized.Contains("digital", StringComparison.OrdinalIgnoreCase))
        {
            return "digital";
        }

        if (normalized.Contains("newspaper", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("print", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("press", StringComparison.OrdinalIgnoreCase))
        {
            return "newspaper";
        }

        return normalized;
    }

    internal static string FormatCurrency(decimal amount)
    {
        return CurrencyFormatSupport.FormatZar(amount);
    }

    internal static string ResolveBusinessReference(RecommendationDocumentModel model)
    {
        return !string.IsNullOrWhiteSpace(model.BusinessName)
            ? model.BusinessName.Trim()
            : (!string.IsNullOrWhiteSpace(model.ClientName) ? model.ClientName.Trim() : "this business");
    }

    internal static string FormatChannelLabel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return string.Empty;
        }

        return NormalizeRecommendationChannel(channel) switch
        {
            "ooh" => "Billboards and Digital Screens",
            "radio" => "Radio",
            "digital" => "Digital",
            "tv" => "TV",
            "newspaper" => "Newspaper",
            "studio" => "Creative and studio support",
            _ => ToClientCopy(channel.Trim().Replace("_", " "))
        };
    }

    internal static string ToClientCopy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("Ã¢â‚¬â„¢", "'")
            .Replace("Ã¢â‚¬Ëœ", "'")
            .Replace("Ã¢â‚¬Å“", "\"")
            .Replace("Ã¢â‚¬Â", "\"")
            .Replace("Ã¢â‚¬â€œ", "-")
            .Replace("Ã¢â‚¬â€", "-")
            .Replace("\u00A0", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ").Trim();

        return Regex.Replace(normalized, "\\booh\\b", "Billboards and Digital Screens", RegexOptions.IgnoreCase);
    }

    internal static string TruncateClientCopy(string? value, int maxLength)
    {
        var clean = ToClientCopy(value);
        if (clean.Length <= maxLength)
        {
            return clean;
        }

        return clean[..Math.Max(0, maxLength - 3)].TrimEnd() + "...";
    }
}

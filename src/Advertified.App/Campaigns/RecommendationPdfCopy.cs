using System.Globalization;
using System.Text.RegularExpressions;

namespace Advertified.App.Campaigns;

internal static class RecommendationPdfCopy
{
    internal static string NormalizeRecommendationChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("ooh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("billboard", StringComparison.OrdinalIgnoreCase)
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

        return normalized;
    }

    internal static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
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

        return string.Equals(channel.Trim(), "OOH", StringComparison.OrdinalIgnoreCase)
            ? "Billboards and Digital Screens"
            : ToClientCopy(channel.Trim());
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

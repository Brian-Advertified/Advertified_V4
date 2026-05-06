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

    internal static string RewriteSelectionReason(string? reason)
    {
        var clean = ToClientCopy(reason);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        return clean.Trim() switch
        {
            "Strong geography match" => "Matches the priority market",
            "Good regional alignment" => "Supports the selected region",
            "Close to the business origin" => "Keeps visibility close to the business base",
            "Supports a high-priority target area" => "Strengthens presence in a priority area",
            "Audience profile overlap" => "Matches the audience profile",
            "Language or audience fit" => "Fits the audience and language direction",
            "Matches requested channel mix" => "Strengthens the campaign journey across the selected touchpoints",
            "Supports requested mix target" => "Keeps the plan balanced across awareness and response channels",
            "Fits comfortably within budget" => "Fits the approved investment level",
            "Strong industry and context fit" => "Fits the business context",
            "Supports campaign objective" => "Supports the campaign objective",
            "Strong overall strategic fit" => "Strong strategic fit",
            "Supports premium positioning" => "Supports premium positioning",
            "Useful for faster buying cycles" => "Helps support faster response moments",
            "Supports walk-in footfall" => "Supports walk-in footfall",
            "Useful for broad audience discovery" => "Useful for broad audience discovery",
            "Covers the full requested language mix" => "Covers the requested language mix",
            "Supports the highest-priority requested language" => "Supports the priority language group",
            "Fixed supplier package investment" => "Uses a bundled media package for simpler planning",
            "Per-spot rate card pricing" => "Allows flexible spot scheduling",
            "High-impact radio daypart" => "Runs in a high-attention radio moment",
            "Supports higher-band radio policy" => "Extends radio reach for this budget level",
            "Builds broad visual reach" => "Builds broad visual reach",
            "Adds strong in-market visibility" => "Adds strong in-market visibility",
            "Supports premium screen presence" => "Supports premium screen presence",
            "Adds contextual retail visibility" => "Adds contextual retail visibility",
            "Premium venue audience fit" => "Fits a premium venue audience",
            "Placed in a premium mall environment" => "Places the campaign in a premium mall environment",
            "Benefits from strong dwell-time environment" => "Benefits from a high-attention dwell-time environment",
            "Strong youth audience signal" => "Signals strong youth audience fit",
            "Strong family shopper signal" => "Signals strong family shopper fit",
            "Strong professional audience signal" => "Signals strong professional audience fit",
            "Billboards and Digital Screens prioritized for visibility" => "Adds strong visual presence in-market",
            "Adds visible market presence" => "Keeps the brand visible in the target area",
            _ => clean.Trim()
        };
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

using System.Globalization;
using System.Text.RegularExpressions;

namespace Advertified.App.Campaigns;

internal static class RecommendationPdfPresentationBuilder
{
    internal static IReadOnlyList<(string Channel, int Quantity)> BuildProposalMediaCounts(RecommendationProposalDocumentModel proposal)
    {
        return proposal.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Channel) && !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => RecommendationPdfCopy.FormatChannelLabel(item.Channel))
            .Select(group =>
            {
                var quantity = group.Sum(item => item.Quantity > 0 ? item.Quantity : 1);
                return (Channel: group.Key, Quantity: quantity);
            })
            .OrderBy(item => item.Channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<(string Key, string Label, decimal Percent)> BuildChannelSpendSplit(RecommendationProposalDocumentModel proposal)
    {
        var channelSpend = proposal.Items
            .Where(item => item.TotalCost > 0 && !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => NormalizeSpendChannel(item.Channel))
            .Select(group => new
            {
                Key = group.Key,
                Total = group.Sum(item => item.TotalCost)
            })
            .Where(entry => entry.Total > 0)
            .ToArray();

        var total = channelSpend.Sum(entry => entry.Total);
        if (total <= 0)
        {
            return Array.Empty<(string Key, string Label, decimal Percent)>();
        }

        return channelSpend
            .Select(entry =>
            {
                var label = entry.Key switch
                {
                    "ooh" => "Billboards and Digital Screens",
                    "radio" => "Radio",
                    "digital" => "Digital (online)",
                    "tv" => "TV",
                    "newspaper" => "Newspaper",
                    _ => RecommendationPdfCopy.ToClientCopy(entry.Key)
                };
                var percent = Math.Round((entry.Total / total) * 100m, 0, MidpointRounding.AwayFromZero);
                return (entry.Key, label, percent);
            })
            .OrderByDescending(entry => entry.percent)
            .Select(entry => (entry.Key, entry.label, entry.percent))
            .ToArray();
    }

    internal static string BuildProposalDeliverableSummary(RecommendationProposalDocumentModel proposal)
    {
        var placements = proposal.Items
            .Where(item => !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (placements.Length == 0)
        {
            return "Proposal details are being prepared.";
        }

        var totalQuantity = placements.Sum(item => Math.Max(1, item.Quantity));
        var channels = placements
            .Select(item => RecommendationPdfCopy.FormatChannelLabel(item.Channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (channels.Length == 1)
        {
            return $"{totalQuantity} planned media placement{(totalQuantity == 1 ? string.Empty : "s")} using {channels[0].ToLowerInvariant()}.";
        }

        var channelSummary = channels.Length == 2
            ? $"{channels[0].ToLowerInvariant()} and {channels[1].ToLowerInvariant()}"
            : $"{string.Join(", ", channels.Take(channels.Length - 1).Select(static value => value.ToLowerInvariant()))}, and {channels[^1].ToLowerInvariant()}";

        return $"{totalQuantity} planned media placement{(totalQuantity == 1 ? string.Empty : "s")} using {channelSummary}.";
    }

    internal static string BuildProposalAreaSummary(RecommendationDocumentModel model, RecommendationProposalDocumentModel proposal)
    {
        var areas = proposal.Items
            .Select(item => item.Region)
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(area => RecommendationPdfCopy.ToClientCopy(area!))
            .ToArray();

        return areas.Length > 0
            ? string.Join(" | ", areas)
            : (model.TargetAreas.Count > 0 ? string.Join(", ", model.TargetAreas.Select(RecommendationPdfCopy.ToClientCopy)) : "South Africa");
    }

    internal static IReadOnlyList<string> BuildProposalPlacementHighlights(RecommendationProposalDocumentModel proposal)
    {
        return proposal.Items
            .Where(item => !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => RecommendationPdfCopy.ToClientCopy(item.Title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    internal static IReadOnlyList<PlacementSectionDocumentModel> BuildPlacementSections(RecommendationProposalDocumentModel proposal)
    {
        return proposal.Items
            .Where(item => !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => RecommendationPdfCopy.NormalizeRecommendationChannel(item.Channel))
            .OrderBy(group => GetChannelSortOrder(group.Key))
            .Select(group =>
            {
                var items = group
                    .OrderBy(GetPlacementPriority)
                    .ThenBy(item => RecommendationPdfCopy.ToClientCopy(item.Title), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new PlacementSectionDocumentModel(
                    GetPlacementSectionLabel(group.Key),
                    items);
            })
            .ToArray();
    }

    internal static string BuildPlacementLocation(RecommendationLineDocumentModel item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Region))
        {
            parts.Add(RecommendationPdfCopy.ToClientCopy(item.Region));
        }

        if (RecommendationPdfCopy.NormalizeRecommendationChannel(item.Channel) == "radio")
        {
            if (!string.IsNullOrWhiteSpace(item.TimeBand))
            {
                parts.Add(RecommendationPdfCopy.ToClientCopy(item.TimeBand));
            }
            else if (!string.IsNullOrWhiteSpace(item.ShowDaypart))
            {
                parts.Add(RecommendationPdfCopy.ToClientCopy(item.ShowDaypart));
            }
        }

        if (!string.IsNullOrWhiteSpace(item.ResolvedStartDate) || !string.IsNullOrWhiteSpace(item.ResolvedEndDate))
        {
            var dateLabel = (item.ResolvedStartDate, item.ResolvedEndDate) switch
            {
                ({ Length: > 0 } start, { Length: > 0 } end) => $"{RecommendationPdfCopy.ToClientCopy(start)} to {RecommendationPdfCopy.ToClientCopy(end)}",
                ({ Length: > 0 } start, _) => $"From {RecommendationPdfCopy.ToClientCopy(start)}",
                (_, { Length: > 0 } end) => $"Until {RecommendationPdfCopy.ToClientCopy(end)}",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(dateLabel))
            {
                parts.Add(dateLabel);
            }
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : RecommendationPdfCopy.ToClientCopy(item.Rationale);
    }

    internal static string BuildPlacementDeliverable(RecommendationLineDocumentModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return RecommendationPdfCopy.ToClientCopy(item.Title);
        }

        if (!string.IsNullOrWhiteSpace(item.SlotType))
        {
            return RecommendationPdfCopy.ToClientCopy(item.SlotType);
        }

        return RecommendationPdfCopy.FormatChannelLabel(item.Channel);
    }

    internal static string BuildPlacementAreaSummary(RecommendationLineDocumentModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.Region))
        {
            return RecommendationPdfCopy.ToClientCopy(item.Region);
        }

        return RecommendationPdfCopy.NormalizeRecommendationChannel(item.Channel) switch
        {
            "digital" => "Online delivery",
            "radio" => "Selected station coverage area",
            "tv" => "Selected broadcast coverage area",
            "ooh" => "Site location is reflected in the placement name",
            _ => "Placement area to be confirmed"
        };
    }

    internal static string? BuildPlacementTimingSummary(RecommendationLineDocumentModel item)
    {
        static string? BuildDateRange(string? start, string? end)
        {
            return (start, end) switch
            {
                ({ Length: > 0 } from, { Length: > 0 } to) => $"{RecommendationPdfCopy.ToClientCopy(from)} to {RecommendationPdfCopy.ToClientCopy(to)}",
                ({ Length: > 0 } from, _) => $"From {RecommendationPdfCopy.ToClientCopy(from)}",
                (_, { Length: > 0 } to) => $"Until {RecommendationPdfCopy.ToClientCopy(to)}",
                _ => null
            };
        }

        return BuildDateRange(item.ResolvedStartDate, item.ResolvedEndDate)
            ?? BuildDateRange(item.RequestedStartDate, item.RequestedEndDate)
            ?? (!string.IsNullOrWhiteSpace(item.AppliedDuration) ? RecommendationPdfCopy.ToClientCopy(item.AppliedDuration) : null)
            ?? (!string.IsNullOrWhiteSpace(item.Flighting) ? RecommendationPdfCopy.ToClientCopy(item.Flighting) : null)
            ?? (!string.IsNullOrWhiteSpace(item.Duration) ? RecommendationPdfCopy.ToClientCopy(item.Duration) : null);
    }

    internal static IReadOnlyList<string> BuildPlacementTags(RecommendationLineDocumentModel item)
    {
        var tags = new List<string>();
        tags.AddRange(item.SelectionReasons.Select(RecommendationPdfCopy.ToClientCopy));

        if (!string.IsNullOrWhiteSpace(item.Language))
        {
            tags.Add(RecommendationPdfCopy.ToClientCopy(item.Language));
        }

        if (!string.IsNullOrWhiteSpace(item.ShowDaypart))
        {
            tags.Add(RecommendationPdfCopy.ToClientCopy(item.ShowDaypart));
        }

        if (!string.IsNullOrWhiteSpace(item.AppliedDuration))
        {
            tags.Add(RecommendationPdfCopy.ToClientCopy(item.AppliedDuration));
        }

        return tags
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    internal static IReadOnlyList<string> BuildClientSelectionSummary(RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var lines = new List<string>();

        var audience = BuildAudienceFocus(model, item);
        if (!string.IsNullOrWhiteSpace(audience))
        {
            lines.Add($"Who we are targeting: {RecommendationPdfCopy.ToClientCopy(audience)}");
        }

        var scale = BuildAudienceScale(item);
        if (!string.IsNullOrWhiteSpace(scale))
        {
            lines.Add($"Estimated audience size: {RecommendationPdfCopy.ToClientCopy(scale)}");
        }

        if (!string.IsNullOrWhiteSpace(item.CommercialExplanation))
        {
            lines.Add($"Commercial fit: {RecommendationPdfCopy.ToClientCopy(item.CommercialExplanation)}");
        }

        var fit = BuildFitNarrative(model, item);
        if (!string.IsNullOrWhiteSpace(fit))
        {
            lines.Add($"Why this fits: {RecommendationPdfCopy.ToClientCopy(fit)}");
        }

        return lines;
    }

    private static string NormalizeSpendChannel(string? channel)
    {
        var normalized = RecommendationPdfCopy.NormalizeRecommendationChannel(channel);
        return normalized switch
        {
            "ooh" => "ooh",
            "radio" => "radio",
            "digital" => "digital",
            "tv" => "tv",
            _ => normalized
        };
    }

    private static int GetChannelSortOrder(string? channel)
    {
        return RecommendationPdfCopy.NormalizeRecommendationChannel(channel) switch
        {
            "ooh" => 0,
            "radio" => 1,
            "tv" => 2,
            "digital" => 3,
            "newspaper" => 4,
            _ => 9
        };
    }

    private static string GetPlacementSectionLabel(string? channel)
    {
        return RecommendationPdfCopy.NormalizeRecommendationChannel(channel) switch
        {
            "ooh" => "Billboards and Digital Screens",
            "radio" => "Radio",
            "tv" => "TV",
            "digital" => "Digital",
            "newspaper" => "Newspaper",
            _ => RecommendationPdfCopy.ToClientCopy(channel)
        };
    }

    private static int GetPlacementPriority(RecommendationLineDocumentModel item)
    {
        var normalizedChannel = RecommendationPdfCopy.NormalizeRecommendationChannel(item.Channel);
        if (!string.Equals(normalizedChannel, "ooh", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var title = (item.Title ?? string.Empty).Trim();
        if (title.Contains("billboard", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (title.Contains("digital screen", StringComparison.OrdinalIgnoreCase)
            || title.Contains("digital screens", StringComparison.OrdinalIgnoreCase)
            || title.Contains("screen", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string? BuildAudienceFocus(RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.TargetAudience))
        {
            parts.Add(item.TargetAudience.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(model.TargetAudienceSummary))
        {
            parts.Add(model.TargetAudienceSummary.Trim());
        }

        var qualifiers = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Language))
        {
            qualifiers.Add($"{item.Language.Trim()} speakers");
        }

        if (!string.IsNullOrWhiteSpace(item.AudienceAgeSkew))
        {
            qualifiers.Add(item.AudienceAgeSkew.Trim());
        }

        if (!string.IsNullOrWhiteSpace(item.AudienceLsmRange))
        {
            qualifiers.Add($"LSM {item.AudienceLsmRange.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(item.AudienceGenderSkew))
        {
            qualifiers.Add(item.AudienceGenderSkew.Trim());
        }

        if (parts.Count == 0 && qualifiers.Count == 0)
        {
            return null;
        }

        if (parts.Count == 0)
        {
            return string.Join(" | ", qualifiers);
        }

        if (qualifiers.Count == 0)
        {
            return string.Join(" | ", parts);
        }

        return $"{string.Join(" | ", parts)} | {string.Join(" | ", qualifiers)}";
    }

    private static string? BuildAudienceScale(RecommendationLineDocumentModel item)
    {
        if (TryFormatWholeNumber(item.ListenershipWeekly, out var weekly))
        {
            var period = !string.IsNullOrWhiteSpace(item.ListenershipPeriod)
                ? $" {item.ListenershipPeriod.Trim().ToLowerInvariant()}"
                : " weekly";
            return $"Approximately {weekly} listeners{period}.";
        }

        if (TryFormatWholeNumber(item.ListenershipDaily, out var daily))
        {
            return $"Approximately {daily} listeners per day.";
        }

        if (TryFormatWholeNumber(item.TrafficCount, out var traffic))
        {
            return $"Approximately {traffic} people pass this site.";
        }

        return null;
    }

    private static string? BuildFitNarrative(RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var fitParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Region) && model.TargetAreas.Count > 0)
        {
            fitParts.Add("It gives us visibility in one of the campaign's target areas");
        }

        if (!string.IsNullOrWhiteSpace(item.Language) && model.TargetLanguages.Count > 0)
        {
            fitParts.Add("It supports the language mix requested for the campaign");
        }

        if (!string.IsNullOrWhiteSpace(item.TimeBand))
        {
            fitParts.Add($"It places the campaign in the {item.TimeBand.Trim()} window");
        }

        if (!string.IsNullOrWhiteSpace(item.SelectionReasons.FirstOrDefault()))
        {
            fitParts.Add(RewriteSelectionReason(item.SelectionReasons.First()));
        }
        else if (!string.IsNullOrWhiteSpace(item.Rationale))
        {
            fitParts.Add(item.Rationale.Trim());
        }

        var cleaned = fitParts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length == 0 ? null : string.Join(". ", cleaned) + ".";
    }

    private static string RewriteSelectionReason(string reason)
    {
        return reason.Trim() switch
        {
            "Strong geography match" => "The location matches the market we want to reach",
            "Good regional alignment" => "The placement lines up well with the target region",
            "Audience profile overlap" => "The audience profile matches the people this campaign is trying to reach",
            "Language or audience fit" => "The audience and language fit the brief",
            "Matches requested channel mix" => "It strengthens the channel mix chosen for this campaign",
            "Supports requested mix target" => "It helps keep the campaign balanced across the selected channels",
            "Fits comfortably within budget" => "It stays comfortably within the approved budget",
            "Fixed supplier package investment" => "It comes as a bundled media package that keeps planning simple",
            "Per-spot rate card pricing" => "It uses standard spot pricing for flexible scheduling",
            "High-impact radio daypart" => "It runs in a high-attention radio slot",
            "Supports higher-band radio policy" => "It supports broader radio reach at this budget level",
            "Billboards and Digital Screens prioritized for visibility" => "It adds strong visual presence in-market",
            "Adds visible market presence" => "It helps the brand stay visible in the target area",
            _ => reason.Trim()
        };
    }

    private static bool TryFormatWholeNumber(string? value, out string formatted)
    {
        formatted = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = Regex.Replace(value, "[^0-9]", string.Empty);
        if (!long.TryParse(digits, out var number) || number <= 0)
        {
            return false;
        }

        formatted = number.ToString("N0", CultureInfo.GetCultureInfo("en-ZA"));
        return true;
    }
}

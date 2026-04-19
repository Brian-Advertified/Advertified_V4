using System.Text.RegularExpressions;

namespace Advertified.App.Campaigns;

internal static class RecommendationClientDocumentShaper
{
    internal static RecommendationProposalDocumentModel ShapeProposal(RecommendationProposalDocumentModel proposal)
    {
        var studioItems = proposal.Items
            .Where(item => string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var mediaItems = proposal.Items
            .Where(item => !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (mediaItems.Length == 0)
        {
            return proposal;
        }

        var shapedItems = CollapseOohVenuePlacements(mediaItems)
            .ToArray();
        shapedItems = CollapseRadioStationSchedules(shapedItems)
            .ToArray();

        var finalItems = shapedItems
            .Concat(studioItems)
            .ToArray();

        return new RecommendationProposalDocumentModel
        {
            Label = proposal.Label,
            Strategy = proposal.Strategy,
            AcceptUrl = proposal.AcceptUrl,
            Summary = proposal.Summary,
            Rationale = proposal.Rationale,
            TotalCost = proposal.TotalCost,
            Items = finalItems
        };
    }

    private static IEnumerable<RecommendationLineDocumentModel> CollapseOohVenuePlacements(IEnumerable<RecommendationLineDocumentModel> items)
    {
        var materialized = items.ToArray();
        if (materialized.Length <= 1)
        {
            return materialized;
        }

        var collapsed = new List<RecommendationLineDocumentModel>();
        var venueGroups = materialized
            .Where(IsGroupableOohPlacement)
            .Select(item => new
            {
                Item = item,
                Venue = ExtractOohVenue(item.Title)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Venue))
            .GroupBy(entry => entry.Venue!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Item).ToArray(), StringComparer.OrdinalIgnoreCase);

        var consumedVenueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in materialized)
        {
            if (!IsGroupableOohPlacement(item))
            {
                collapsed.Add(item);
                continue;
            }

            var venue = ExtractOohVenue(item.Title);
            if (string.IsNullOrWhiteSpace(venue)
                || !venueGroups.TryGetValue(venue, out var groupedItems)
                || groupedItems.Length <= 1)
            {
                collapsed.Add(item);
                continue;
            }

            if (!consumedVenueKeys.Add(venue))
            {
                continue;
            }

            collapsed.Add(BuildCollapsedOohPlacement(venue!, groupedItems));
        }

        return collapsed;
    }

    private static IEnumerable<RecommendationLineDocumentModel> CollapseRadioStationSchedules(IEnumerable<RecommendationLineDocumentModel> items)
    {
        var materialized = items.ToArray();
        if (materialized.Length <= 1)
        {
            return materialized;
        }

        var collapsed = new List<RecommendationLineDocumentModel>();
        var radioGroups = materialized
            .Where(CanGroupRadioPlacement)
            .Select(item => new
            {
                Item = item,
                Station = ExtractStationName(item.Title)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Station))
            .GroupBy(entry => entry.Station!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Item).ToArray(), StringComparer.OrdinalIgnoreCase);

        var consumedStations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in materialized)
        {
            if (!CanGroupRadioPlacement(item))
            {
                collapsed.Add(item);
                continue;
            }

            var station = ExtractStationName(item.Title);
            if (string.IsNullOrWhiteSpace(station)
                || !radioGroups.TryGetValue(station, out var groupedItems)
                || groupedItems.Length <= 1)
            {
                collapsed.Add(item);
                continue;
            }

            if (!consumedStations.Add(station))
            {
                continue;
            }

            collapsed.Add(BuildCollapsedRadioPlacement(station!, groupedItems));
        }

        return collapsed;
    }

    private static RecommendationLineDocumentModel BuildCollapsedOohPlacement(string venue, IReadOnlyList<RecommendationLineDocumentModel> items)
    {
        var totalPlacements = items.Sum(item => Math.Max(1, item.Quantity));
        var region = items
            .Select(item => item.Region)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var duration = items
            .Select(item => item.Duration)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var reasons = items
            .SelectMany(item => item.SelectionReasons)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rationale = items
            .Select(item => item.Rationale)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;

        return new RecommendationLineDocumentModel
        {
            Channel = items[0].Channel,
            Title = $"{totalPlacements} placement{(totalPlacements == 1 ? string.Empty : "s")} at {venue}",
            Rationale = rationale,
            TotalCost = items.Sum(item => item.TotalCost),
            Quantity = totalPlacements,
            Region = region,
            Duration = duration,
            SelectionReasons = reasons
        };
    }

    private static RecommendationLineDocumentModel BuildCollapsedRadioPlacement(string station, IReadOnlyList<RecommendationLineDocumentModel> items)
    {
        var totalSpots = items.Sum(item => Math.Max(1, item.Quantity));
        var region = items
            .Select(item => item.Region)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var language = items
            .Select(item => item.Language)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var duration = items
            .Select(item => item.Duration)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var slotList = items
            .Select(BuildRadioSlotLabel)
            .Where(slot => !string.IsNullOrWhiteSpace(slot))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        var reasons = items
            .SelectMany(item => item.SelectionReasons)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (slotList.Length > 0)
        {
            reasons.Insert(0, $"Slots: {string.Join(", ", slotList)}");
        }

        var rationale = items
            .Select(item => item.Rationale)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "Selected for repeated reach on the station.";

        return new RecommendationLineDocumentModel
        {
            Channel = items[0].Channel,
            Title = $"{station} radio schedule",
            Rationale = rationale,
            TotalCost = items.Sum(item => item.TotalCost),
            Quantity = totalSpots,
            Region = region,
            Language = language,
            Duration = duration,
            SelectionReasons = reasons.ToArray()
        };
    }

    private static bool CanGroupRadioPlacement(RecommendationLineDocumentModel item)
    {
        if (!string.Equals(NormalizeChannel(item.Channel), "radio", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return false;
        }

        return item.Title.Contains(" - ", StringComparison.Ordinal);
    }

    private static string BuildRadioSlotLabel(RecommendationLineDocumentModel item)
    {
        var title = item.Title?.Trim() ?? string.Empty;
        var station = ExtractStationName(title);
        var fromTitle = !string.IsNullOrWhiteSpace(station) && title.StartsWith(station + " - ", StringComparison.OrdinalIgnoreCase)
            ? title[(station.Length + 3)..].Trim()
            : title;

        var preferred = FirstNonEmpty(item.ShowDaypart, item.TimeBand, fromTitle);
        return preferred switch
        {
            null => string.Empty,
            _ => preferred
        };
    }

    private static string? ExtractStationName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var separatorIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        return title[..separatorIndex].Trim();
    }

    private static bool IsGroupableOohPlacement(RecommendationLineDocumentModel item)
    {
        if (!string.Equals(NormalizeChannel(item.Channel), "ooh", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(item.Title))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(ExtractOohVenue(item.Title));
    }

    private static string? ExtractOohVenue(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = NormalizeClientCopy(title).Trim();
        normalized = StripKnownOohSuffix(normalized);
        normalized = normalized.Replace(",,", ",");
        normalized = Regex.Replace(normalized, "\\s*,\\s*", ", ");
        normalized = normalized.TrimEnd('-', '|', ',', ' ');
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string StripKnownOohSuffix(string normalized)
    {
        var suffixes = new[]
        {
            "digital screen",
            "billboard",
            "static entrance wall | outdoor",
            "static escalator wrap | indoor",
            "static glass balustrades | indoor",
            "static hanging banner | indoor",
            "static lift door wrap | indoor",
            "static lift panels | indoor",
            "static overhead banner | indoor",
            "static parking gantry | outdoor",
            "static parking wall | outdoor",
            "static pillar wrap | indoor",
            "static wall banner | indoor",
            "static elevator wrap | indoor",
            "static taxi rank entrance banner | indoor"
        };

        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return normalized[..^suffix.Length].Trim().TrimEnd('-', '|', ',', ' ');
            }
        }

        return normalized;
    }

    private static string NormalizeChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("ooh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("billboard", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("digital screen", StringComparison.OrdinalIgnoreCase)
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

    private static string NormalizeClientCopy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢", "'")
            .Replace("ÃƒÂ¢Ã¢â€šÂ¬Ã‹Å“", "'")
            .Replace("ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œ", "\"")
            .Replace("ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â", "\"")
            .Replace("ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“", "-")
            .Replace("ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â", "-")
            .Replace("\u00A0", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ").Trim();
        return normalized;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}

using System.Text.Json;
using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Support;

public static class CampaignFlightingSupport
{
    public static List<CampaignChannelFlightRequest> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CampaignChannelFlightRequest>();
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<List<CampaignChannelFlightRequest>>(json));
        }
        catch (JsonException)
        {
            return new List<CampaignChannelFlightRequest>();
        }
    }

    public static List<CampaignChannelFlightRequest> Normalize(
        IEnumerable<CampaignChannelFlightRequest>? flights,
        DateOnly? fallbackStartDate = null,
        DateOnly? fallbackEndDate = null,
        int? fallbackDurationWeeks = null)
    {
        if (flights is null)
        {
            return new List<CampaignChannelFlightRequest>();
        }

        return flights
            .Where(static flight => flight is not null && !string.IsNullOrWhiteSpace(flight.Channel))
            .Select(flight => NormalizeFlight(flight, fallbackStartDate, fallbackEndDate, fallbackDurationWeeks))
            .GroupBy(flight => flight.Channel, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public static string NormalizeChannel(string? channel)
    {
        return PlanningChannelSupport.NormalizeChannel(channel);
    }

    private static CampaignChannelFlightRequest NormalizeFlight(
        CampaignChannelFlightRequest flight,
        DateOnly? fallbackStartDate,
        DateOnly? fallbackEndDate,
        int? fallbackDurationWeeks)
    {
        var normalizedChannel = NormalizeChannel(flight.Channel);
        var normalizedStart = flight.StartDate ?? fallbackStartDate;
        var normalizedEnd = flight.EndDate ?? fallbackEndDate;
        var normalizedDurationWeeks = flight.DurationWeeks;
        var normalizedDurationMonths = flight.DurationMonths;

        if (!normalizedEnd.HasValue && normalizedStart.HasValue)
        {
            if (normalizedDurationMonths.HasValue && normalizedDurationMonths.Value > 0)
            {
                normalizedEnd = normalizedStart.Value.AddMonths(normalizedDurationMonths.Value).AddDays(-1);
            }
            else if (normalizedDurationWeeks.HasValue && normalizedDurationWeeks.Value > 0)
            {
                normalizedEnd = normalizedStart.Value.AddDays((normalizedDurationWeeks.Value * 7) - 1);
            }
            else if (fallbackEndDate.HasValue)
            {
                normalizedEnd = fallbackEndDate;
            }
        }

        if (!normalizedDurationWeeks.HasValue && normalizedStart.HasValue && normalizedEnd.HasValue && normalizedEnd.Value >= normalizedStart.Value)
        {
            normalizedDurationWeeks = Math.Max(1, (int)Math.Ceiling((normalizedEnd.Value.DayNumber - normalizedStart.Value.DayNumber + 1) / 7d));
        }

        if (!normalizedDurationMonths.HasValue && normalizedStart.HasValue && normalizedEnd.HasValue && normalizedEnd.Value >= normalizedStart.Value)
        {
            normalizedDurationMonths = Math.Max(1, CalculateCalendarMonths(normalizedStart.Value, normalizedEnd.Value));
        }

        if (!normalizedDurationWeeks.HasValue)
        {
            normalizedDurationWeeks = fallbackDurationWeeks;
        }

        return new CampaignChannelFlightRequest
        {
            Channel = normalizedChannel,
            StartDate = normalizedStart,
            EndDate = normalizedEnd,
            DurationWeeks = normalizedDurationWeeks,
            DurationMonths = normalizedDurationMonths,
            Priority = flight.Priority,
            Notes = string.IsNullOrWhiteSpace(flight.Notes) ? null : flight.Notes.Trim()
        };
    }

    private static int CalculateCalendarMonths(DateOnly start, DateOnly end)
    {
        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        if (end.Day >= start.Day)
        {
            months++;
        }

        return months <= 0 ? 1 : months;
    }
}

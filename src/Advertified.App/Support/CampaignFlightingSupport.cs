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
            return JsonSerializer.Deserialize<List<CampaignChannelFlightRequest>>(json) ?? new List<CampaignChannelFlightRequest>();
        }
        catch (JsonException)
        {
            return new List<CampaignChannelFlightRequest>();
        }
    }

    public static List<CampaignChannelFlightRequest> Normalize(
        IEnumerable<CampaignChannelFlightRequest>? flights,
        DateOnly? fallbackStartDate,
        DateOnly? fallbackEndDate,
        int? fallbackDurationWeeks)
    {
        var explicitFlights = (flights ?? Array.Empty<CampaignChannelFlightRequest>())
            .Where(flight => !string.IsNullOrWhiteSpace(flight.Channel))
            .Select(flight => new CampaignChannelFlightRequest
            {
                Channel = NormalizeChannel(flight.Channel),
                StartDate = flight.StartDate,
                EndDate = flight.EndDate,
                DurationWeeks = flight.DurationWeeks,
                DurationMonths = flight.DurationMonths,
                Priority = flight.Priority,
                Notes = string.IsNullOrWhiteSpace(flight.Notes) ? null : flight.Notes.Trim()
            })
            .GroupBy(flight => flight.Channel, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(flight => flight.Priority ?? int.MaxValue)
                .ThenBy(flight => flight.Channel, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToList();

        if (explicitFlights.Count > 0)
        {
            return explicitFlights;
        }

        return new List<CampaignChannelFlightRequest>();
    }

    public static string NormalizeChannel(string? value)
    {
        if (PlanningChannelSupport.IsOohFamilyChannel(value))
        {
            return PlanningChannelSupport.OohAlias;
        }

        return PlanningChannelSupport.NormalizeChannel(value);
    }
}

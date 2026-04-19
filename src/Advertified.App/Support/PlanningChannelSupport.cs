namespace Advertified.App.Support;

public static class PlanningChannelSupport
{
    public const string Billboard = "billboard";
    public const string DigitalScreen = "digital_screen";
    public const string Radio = "radio";
    public const string Digital = "digital";
    public const string Tv = "tv";
    public const string OohAlias = "ooh";

    public static string NormalizeChannel(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "television" => Tv,
            "billboards and digital screens" => OohAlias,
            "billboards" => Billboard,
            "billboard" => Billboard,
            "digital screens" => DigitalScreen,
            "digital screen" => DigitalScreen,
            "screen" => DigitalScreen,
            "screens" => DigitalScreen,
            "out of home" => OohAlias,
            _ => normalized
        };
    }

    public static bool IsOohFamilyChannel(string? value)
    {
        var normalized = NormalizeChannel(value);
        return normalized is OohAlias or Billboard or DigitalScreen;
    }

    public static IReadOnlyList<string> ExpandRequestedChannel(string? value)
    {
        var normalized = NormalizeChannel(value);
        return normalized switch
        {
            OohAlias => new[] { Billboard, DigitalScreen },
            Billboard => new[] { Billboard },
            DigitalScreen => new[] { DigitalScreen },
            Radio => new[] { Radio },
            Digital => new[] { Digital },
            Tv => new[] { Tv },
            _ when string.IsNullOrWhiteSpace(normalized) => Array.Empty<string>(),
            _ => new[] { normalized }
        };
    }

    public static string GetDisplayLabel(string? value)
    {
        return NormalizeChannel(value) switch
        {
            Billboard => "Billboards",
            DigitalScreen => "Digital Screens",
            Radio => "Radio",
            Digital => "Digital",
            Tv => "TV",
            OohAlias => "Billboards or Digital Screens",
            _ => value ?? string.Empty
        };
    }

    public static string ClassifyOohChannel(string? subtype, string? slotType, string? displayName)
    {
        var tokens = string.Join(" | ", new[] { subtype, slotType, displayName }
            .Where(static value => !string.IsNullOrWhiteSpace(value)))
            .Trim()
            .ToLowerInvariant();

        if (tokens.Contains("screen", StringComparison.OrdinalIgnoreCase))
        {
            return DigitalScreen;
        }

        return Billboard;
    }

    public static bool MatchesRequestedChannel(string? candidateChannel, string? requestedChannel)
    {
        var normalizedCandidate = NormalizeChannel(candidateChannel);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return false;
        }

        return ExpandRequestedChannel(requestedChannel)
            .Any(channel => string.Equals(channel, normalizedCandidate, StringComparison.OrdinalIgnoreCase));
    }
}

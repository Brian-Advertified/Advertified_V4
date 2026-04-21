using System.Globalization;
using System.Text.RegularExpressions;

namespace Advertified.App.Support;

public static partial class GpsCoordinateParser
{
    public static (double Latitude, double Longitude)? TryParse(string? rawCoordinates)
    {
        if (string.IsNullOrWhiteSpace(rawCoordinates))
        {
            return null;
        }

        var decimalCoordinates = TryParseDecimalCoordinates(rawCoordinates);
        if (decimalCoordinates.HasValue)
        {
            return decimalCoordinates;
        }

        return TryParseDmsCoordinates(rawCoordinates);
    }

    private static (double Latitude, double Longitude)? TryParseDecimalCoordinates(string rawCoordinates)
    {
        var normalized = Normalize(rawCoordinates);
        var match = DecimalCoordinatesRegex().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(match.Groups[2].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        if (latitude is < -90d or > 90d || longitude is < -180d or > 180d)
        {
            return null;
        }

        return (latitude, longitude);
    }

    private static (double Latitude, double Longitude)? TryParseDmsCoordinates(string rawCoordinates)
    {
        var normalized = Normalize(rawCoordinates);
        var matches = DmsCoordinateRegex().Matches(normalized);
        if (matches.Count < 2)
        {
            return null;
        }

        var latitude = ParseDmsCoordinate(matches[0]);
        var longitude = ParseDmsCoordinate(matches[1]);
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        return (latitude.Value, longitude.Value);
    }

    private static double? ParseDmsCoordinate(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var degrees)
            || !double.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes)
            || !double.TryParse(match.Groups[3].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var hemisphere = match.Groups[4].Value.ToUpperInvariant();
        var decimalDegrees = degrees + (minutes / 60d) + (seconds / 3600d);
        if (hemisphere is "S" or "W")
        {
            decimalDegrees *= -1d;
        }

        return decimalDegrees switch
        {
            < -180d or > 180d => null,
            _ => decimalDegrees
        };
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .Replace("Notes:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Animation:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("â€", "\"", StringComparison.Ordinal)
            .Replace("â€œ", "\"", StringComparison.Ordinal)
            .Replace("â€™", "'", StringComparison.Ordinal)
            .Replace("â€˜", "'", StringComparison.Ordinal)
            .Replace("Â°", "°", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(-?\d{1,2}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DecimalCoordinatesRegex();

    [GeneratedRegex(@"(\d{1,3})[^0-9A-Z]+(\d{1,2})[^0-9A-Z]+(\d{1,2}(?:\.\d+)?)\D*([NSEW])", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DmsCoordinateRegex();
}

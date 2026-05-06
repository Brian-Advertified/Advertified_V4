using Advertified.App.Contracts.Packages;
using Advertified.App.Services.Abstractions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Advertified.App.Services;

public sealed class PackagePreviewOutdoorSelector : IPackagePreviewOutdoorSelector
{
    public IReadOnlyList<OohPreviewRow> SelectExamples(IReadOnlyList<OohPreviewRow> candidates, PackagePreviewAreaProfile selectedArea, decimal budget, decimal budgetRatio)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<OohPreviewRow>();
        }

        var filteredCandidates = selectedArea.Code == "national"
            ? candidates.ToList()
            : candidates.Where(candidate => MatchesArea(candidate, selectedArea)).ToList();
        var targetPlacementCost = budget * (0.08m + (budgetRatio * 0.18m));

        var scoredCandidates = filteredCandidates
            .OrderByDescending(candidate => ScoreCandidate(candidate, targetPlacementCost, budgetRatio))
            .ThenByDescending(candidate => candidate.TrafficCount)
            .Take(12)
            .GroupBy(BuildAreaKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => ScoreCandidate(candidate, targetPlacementCost, budgetRatio))
                .ThenByDescending(candidate => candidate.TrafficCount)
                .First())
            .ToList();

        return scoredCandidates
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();
    }

    public IReadOnlyList<PackagePreviewMapPoint> BuildMapPoints(IReadOnlyList<OohPreviewRow> rows, PackagePreviewAreaProfile selectedArea)
    {
        return rows
            .Select(row =>
            {
                var coordinates = TryParseGpsCoordinates(row.GpsCoordinates);
                if (coordinates is null)
                {
                    return null;
                }

                return new PackagePreviewMapPoint
                {
                    Label = BuildExampleLocationLabel(row),
                    SiteName = row.SiteName,
                    City = row.City,
                    Province = row.Province,
                    Latitude = coordinates.Value.Latitude,
                    Longitude = coordinates.Value.Longitude,
                    IsInSelectedArea = selectedArea.Code == "national" || MatchesArea(row, selectedArea)
                };
            })
            .Where(point => point is not null)
            .DistinctBy(point => $"{point!.SiteName}|{point.Latitude}|{point.Longitude}")
            .Take(180)
            .Select(point => point!)
            .ToList();
    }

    private static decimal ScoreCandidate(OohPreviewRow candidate, decimal targetPlacementCost, decimal budgetRatio)
    {
        var trafficScore = candidate.TrafficCount / 100000m;
        var costDistance = targetPlacementCost <= 0
            ? 1m
            : Math.Abs(candidate.Cost - targetPlacementCost) / targetPlacementCost;
        var affordabilityScore = Math.Max(0m, 10m - (costDistance * 10m));
        var premiumBias = budgetRatio >= 0.6m && candidate.Cost > targetPlacementCost ? 2m : 0m;

        return trafficScore + affordabilityScore + premiumBias;
    }

    private static string BuildAreaKey(OohPreviewRow row)
    {
        return $"{row.Suburb}|{row.City}".Trim().ToLowerInvariant();
    }

    private static bool MatchesArea(OohPreviewRow row, PackagePreviewAreaProfile selectedArea)
    {
        var selectedCode = NormalizeAreaToken(selectedArea.Code);
        var clusterCode = NormalizeAreaToken(row.RegionClusterCode);
        if (!string.IsNullOrWhiteSpace(clusterCode))
        {
            return selectedCode switch
            {
                "national" => true,
                _ => clusterCode == selectedCode
            };
        }

        var province = NormalizeAreaToken(row.Province);
        var city = NormalizeAreaToken(row.City);
        var provinceTerms = selectedArea.ProvinceTerms
            .Append(selectedArea.Name)
            .Append(selectedArea.Code)
            .Select(NormalizeAreaToken)
            .Where(static term => !string.IsNullOrWhiteSpace(term));
        var cityTerms = selectedArea.CityTerms
            .Select(NormalizeAreaToken)
            .Where(static term => !string.IsNullOrWhiteSpace(term));

        return provinceTerms.Any(term => province.Contains(term, StringComparison.OrdinalIgnoreCase))
            || cityTerms.Any(term => city.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAreaToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", string.Empty);
    }

    private static string BuildExampleLocationLabel(OohPreviewRow row)
    {
        var areaLabel = row.Suburb.Equals(row.City, StringComparison.OrdinalIgnoreCase)
            ? row.Suburb
            : $"{row.Suburb}, {row.City}";

        var audienceCue = row.TrafficCount switch
        {
            >= 3000000 => "high-income commuter traffic",
            >= 1200000 => "strong retail and commuter movement",
            >= 600000 => "high foot and vehicle traffic",
            _ => "consistent local visibility"
        };

        return $"{areaLabel} ({audienceCue})";
    }

    private static (decimal Latitude, decimal Longitude)? TryParseGpsCoordinates(string? gpsCoordinates)
    {
        if (string.IsNullOrWhiteSpace(gpsCoordinates))
        {
            return null;
        }

        var decimalCoordinates = TryParseDecimalCoordinates(gpsCoordinates);
        if (decimalCoordinates is not null)
        {
            return decimalCoordinates;
        }

        var normalized = gpsCoordinates
            .Trim()
            .Replace("Notes:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("â€", "\"", StringComparison.Ordinal)
            .Replace("â€œ", "\"", StringComparison.Ordinal)
            .Replace("â€™", "'", StringComparison.Ordinal)
            .Replace("â€˜", "'", StringComparison.Ordinal);

        var matches = Regex.Matches(normalized, @"(\d{1,3})Â°(\d{1,2})['â€²](\d{1,2})[""â€³]?([NSEW])", RegexOptions.IgnoreCase);
        if (matches.Count < 2)
        {
            return null;
        }

        var latitude = ParseDmsCoordinate(matches[0]);
        var longitude = ParseDmsCoordinate(matches[1]);

        if (latitude is null || longitude is null)
        {
            return null;
        }

        return (latitude.Value, longitude.Value);
    }

    private static decimal? ParseDmsCoordinate(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var degrees)
            || !decimal.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes)
            || !decimal.TryParse(match.Groups[3].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var hemisphere = match.Groups[4].Value.ToUpperInvariant();
        var decimalDegrees = degrees + (minutes / 60m) + (seconds / 3600m);

        if (hemisphere is "S" or "W")
        {
            decimalDegrees *= -1m;
        }

        return decimalDegrees;
    }

    private static (decimal Latitude, decimal Longitude)? TryParseDecimalCoordinates(string rawCoordinates)
    {
        var normalized = rawCoordinates
            .Trim()
            .Replace("Notes:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Animation:", string.Empty, StringComparison.OrdinalIgnoreCase);

        var decimalMatch = Regex.Match(
            normalized,
            @"(-?\d{1,2}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)",
            RegexOptions.IgnoreCase);

        if (!decimalMatch.Success)
        {
            return null;
        }

        if (!decimal.TryParse(decimalMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var latitude)
            || !decimal.TryParse(decimalMatch.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            return null;
        }

        return (latitude, longitude);
    }
}

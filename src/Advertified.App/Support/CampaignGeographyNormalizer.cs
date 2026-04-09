namespace Advertified.App.Support;

public static class CampaignGeographyNormalizer
{
    public static NormalizedCampaignGeography Normalize(
        string? scope,
        IReadOnlyList<string>? provinces,
        IReadOnlyList<string>? cities,
        IReadOnlyList<string>? suburbs,
        IReadOnlyList<string>? areas)
    {
        var normalizedScope = NormalizeScope(scope);
        var normalizedProvinces = NormalizeList(provinces);
        var normalizedCities = NormalizeList(cities);
        var normalizedSuburbs = NormalizeList(suburbs);
        var normalizedAreas = NormalizeList(areas);

        if (normalizedScope == "provincial")
        {
            if (normalizedProvinces.Length > 0)
            {
                return new NormalizedCampaignGeography(
                    "provincial",
                    normalizedProvinces,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            if (normalizedCities.Length > 0 || normalizedSuburbs.Length > 0 || normalizedAreas.Length > 0)
            {
                return new NormalizedCampaignGeography(
                    "local",
                    Array.Empty<string>(),
                    normalizedCities,
                    normalizedSuburbs,
                    normalizedAreas);
            }

            return new NormalizedCampaignGeography(
                "provincial",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        if (normalizedScope == "local")
        {
            var hasLocalTargets = normalizedCities.Length > 0 || normalizedSuburbs.Length > 0 || normalizedAreas.Length > 0;
            if (hasLocalTargets)
            {
                return new NormalizedCampaignGeography(
                    "local",
                    Array.Empty<string>(),
                    normalizedCities,
                    normalizedSuburbs,
                    normalizedAreas);
            }
        }

        return new NormalizedCampaignGeography(
            normalizedScope,
            normalizedScope == "provincial" ? normalizedProvinces : Array.Empty<string>(),
            normalizedScope == "local" ? normalizedCities : Array.Empty<string>(),
            normalizedScope == "local" ? normalizedSuburbs : Array.Empty<string>(),
            normalizedScope == "local" ? normalizedAreas : Array.Empty<string>());
    }

    private static string NormalizeScope(string? scope)
    {
        return (scope ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "regional" => "provincial",
            "local" => "local",
            "provincial" => "provincial",
            "national" => "national",
            _ => "provincial"
        };
    }

    private static string[] NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record NormalizedCampaignGeography(
    string Scope,
    IReadOnlyList<string> Provinces,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Suburbs,
    IReadOnlyList<string> Areas);

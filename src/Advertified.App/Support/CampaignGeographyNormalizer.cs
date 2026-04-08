namespace Advertified.App.Support;

public static class CampaignGeographyNormalizer
{
    private static readonly Dictionary<string, string> ProvinceCanonicalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["easterncape"] = "Eastern Cape",
        ["eastern_cape"] = "Eastern Cape",
        ["free_state"] = "Free State",
        ["freestate"] = "Free State",
        ["gauteng"] = "Gauteng",
        ["kwazulu_natal"] = "KwaZulu-Natal",
        ["kwazulunatal"] = "KwaZulu-Natal",
        ["kzn"] = "KwaZulu-Natal",
        ["limpopo"] = "Limpopo",
        ["mpumalanga"] = "Mpumalanga",
        ["north_west"] = "North West",
        ["northwest"] = "North West",
        ["northern_cape"] = "Northern Cape",
        ["northerncape"] = "Northern Cape",
        ["western_cape"] = "Western Cape",
        ["westerncape"] = "Western Cape",
        ["national"] = "National"
    };

    private static readonly Dictionary<string, string> CityToProvince = new(StringComparer.OrdinalIgnoreCase)
    {
        ["johannesburg"] = "Gauteng",
        ["pretoria"] = "Gauteng",
        ["sandton"] = "Gauteng",
        ["randburg"] = "Gauteng",
        ["soweto"] = "Gauteng",
        ["cape town"] = "Western Cape",
        ["bellville"] = "Western Cape",
        ["durban"] = "KwaZulu-Natal",
        ["umhlanga"] = "KwaZulu-Natal",
        ["pietermaritzburg"] = "KwaZulu-Natal",
        ["gqeberha"] = "Eastern Cape",
        ["east london"] = "Eastern Cape",
        ["bloemfontein"] = "Free State",
        ["polokwane"] = "Limpopo",
        ["mbombela"] = "Mpumalanga",
        ["rustenburg"] = "North West"
    };

    private static readonly Dictionary<string, string> AreaToCity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hyde park"] = "Johannesburg",
        ["rosebank"] = "Johannesburg",
        ["fourways"] = "Johannesburg",
        ["midrand"] = "Johannesburg",
        ["centurion"] = "Pretoria"
    };

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
            var validProvinces = normalizedProvinces
                .Select(CanonicalizeProvince)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (validProvinces.Length > 0)
            {
                return new NormalizedCampaignGeography(
                    "provincial",
                    validProvinces,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            var fallbackAreas = normalizedProvinces
                .Concat(normalizedAreas)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var inferredCities = InferCities(fallbackAreas, normalizedCities, normalizedSuburbs);

            if (inferredCities.Length > 0 || fallbackAreas.Length > 0 || normalizedSuburbs.Length > 0)
            {
                return new NormalizedCampaignGeography(
                    "local",
                    Array.Empty<string>(),
                    inferredCities,
                    normalizedSuburbs,
                    fallbackAreas);
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

            var provincialFromCities = normalizedCities
                .Select(CanonicalizeProvinceFromCity)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (provincialFromCities.Length > 0)
            {
                return new NormalizedCampaignGeography(
                    "provincial",
                    provincialFromCities,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
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

    private static string[] InferCities(
        IReadOnlyList<string> areas,
        IReadOnlyList<string> existingCities,
        IReadOnlyList<string> suburbs)
    {
        var inferred = new List<string>(existingCities);

        foreach (var area in areas.Concat(suburbs))
        {
            if (AreaToCity.TryGetValue(area.Trim(), out var city))
            {
                inferred.Add(city);
                continue;
            }

            if (CityToProvince.ContainsKey(area.Trim()))
            {
                inferred.Add(ToTitleCase(area.Trim()));
            }
        }

        return inferred
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? CanonicalizeProvince(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = NormalizeKey(value);
        return ProvinceCanonicalMap.TryGetValue(key, out var canonical)
            ? canonical
            : null;
    }

    private static string? CanonicalizeProvinceFromCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        return CityToProvince.TryGetValue(city.Trim(), out var province)
            ? province
            : null;
    }

    private static string NormalizeKey(string input)
    {
        return input.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
    }

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return string.Join(' ', input
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}

public sealed record NormalizedCampaignGeography(
    string Scope,
    IReadOnlyList<string> Provinces,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Suburbs,
    IReadOnlyList<string> Areas);

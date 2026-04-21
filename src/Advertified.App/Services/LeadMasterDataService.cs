using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class LeadMasterDataService : ILeadMasterDataService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly object _syncRoot = new();
    private LeadMasterDataSnapshot? _snapshot;

    public LeadMasterDataService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public LeadMasterTokenSet GetTokenSet()
    {
        var snapshot = GetSnapshot();
        return new LeadMasterTokenSet
        {
            LocationTokens = snapshot.LocationTokens,
            IndustryTokens = snapshot.IndustryTokens,
            LanguageTokens = snapshot.LanguageTokens
        };
    }

    public MasterLocationMatch? ResolveLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var snapshot = GetSnapshot();
        var normalized = NormalizeToken(value);
        if (!snapshot.LocationAliases.TryGetValue(normalized, out var match))
        {
            return null;
        }

        return new MasterLocationMatch
        {
            CanonicalName = match.CanonicalName,
            Latitude = match.Latitude,
            Longitude = match.Longitude
        };
    }

    public MasterLocationMatch? ResolveNearestLocation(double latitude, double longitude, double maxDistanceKm = 50d)
    {
        var snapshot = GetSnapshot();
        var nearest = snapshot.CanonicalLocations
            .Where(location => location.Latitude.HasValue && location.Longitude.HasValue)
            .Select(location => new
            {
                Location = location,
                DistanceKm = HaversineDistanceKm(latitude, longitude, location.Latitude!.Value, location.Longitude!.Value)
            })
            .Where(entry => entry.DistanceKm <= maxDistanceKm)
            .OrderBy(entry => entry.DistanceKm)
            .FirstOrDefault();

        if (nearest is null)
        {
            return null;
        }

        return new MasterLocationMatch
        {
            CanonicalName = nearest.Location.CanonicalName,
            LocationType = nearest.Location.LocationType,
            ParentCity = nearest.Location.ParentCity,
            Province = nearest.Location.Province,
            Latitude = nearest.Location.Latitude,
            Longitude = nearest.Location.Longitude,
            DistanceKm = nearest.DistanceKm
        };
    }

    public MasterIndustryMatch? ResolveIndustry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var snapshot = GetSnapshot();
        var normalized = NormalizeToken(value);
        if (!snapshot.IndustryAliases.TryGetValue(normalized, out var match))
        {
            return null;
        }

        return new MasterIndustryMatch
        {
            Code = match.Code,
            Label = match.Label
        };
    }

    public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints)
    {
        if (hints.Count == 0)
        {
            return null;
        }

        var snapshot = GetSnapshot();

        foreach (var hint in hints)
        {
            var directMatch = ResolveIndustry(hint);
            if (directMatch is not null)
            {
                return directMatch;
            }

            var normalizedHint = NormalizeToken(hint);
            if (string.IsNullOrWhiteSpace(normalizedHint))
            {
                continue;
            }

            var partialMatch = snapshot.IndustryAliases
                .FirstOrDefault(entry =>
                    normalizedHint.Contains(entry.Key, StringComparison.Ordinal)
                    || entry.Key.Contains(normalizedHint, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(partialMatch.Key))
            {
                return new MasterIndustryMatch
                {
                    Code = partialMatch.Value.Code,
                    Label = partialMatch.Value.Label
                };
            }
        }

        return null;
    }

    public MasterLanguageMatch? ResolveLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var snapshot = GetSnapshot();
        var normalized = NormalizeToken(value);
        if (!snapshot.LanguageAliases.TryGetValue(normalized, out var match))
        {
            return null;
        }

        return new MasterLanguageMatch
        {
            Code = match.Code,
            Label = match.Label
        };
    }

    private LeadMasterDataSnapshot GetSnapshot()
    {
        if (_snapshot is not null)
        {
            return _snapshot;
        }

        lock (_syncRoot)
        {
            _snapshot ??= LoadSnapshot();
            return _snapshot;
        }
    }

    private LeadMasterDataSnapshot LoadSnapshot()
    {
        try
        {
            using var connection = _dataSource.OpenConnection();
            var locationRows = connection.Query<LocationAliasRow>(
                @"select
                    location_aliases.alias,
                    location_aliases.CanonicalName,
                    location_aliases.Latitude,
                    location_aliases.Longitude
                  from (
                    select
                        ml.canonical_name as alias,
                        ml.canonical_name as CanonicalName,
                        ml.latitude as Latitude,
                        ml.longitude as Longitude
                    from master_locations ml
                    union all
                    select
                        mla.alias,
                        ml.canonical_name as CanonicalName,
                        ml.latitude as Latitude,
                        ml.longitude as Longitude
                    from master_location_aliases mla
                    join master_locations ml on ml.id = mla.master_location_id
                  ) location_aliases;");

            var canonicalLocationRows = connection.Query<CanonicalLocationRow>(
                @"select
                    canonical_name as CanonicalName,
                    location_type as LocationType,
                    parent_city as ParentCity,
                    province as Province,
                    latitude as Latitude,
                    longitude as Longitude
                  from master_locations;");

            var industryRows = connection.Query<IndustryAliasRow>(
                @"select
                    industry_aliases.alias,
                    industry_aliases.Code,
                    industry_aliases.Label
                  from (
                    select
                        mi.label as alias,
                        mi.code as Code,
                        mi.label as Label
                    from master_industries mi
                    union all
                    select
                        mi.code as alias,
                        mi.code as Code,
                        mi.label as Label
                    from master_industries mi
                    union all
                    select
                        mia.alias,
                        mi.code as Code,
                        mi.label as Label
                    from master_industry_aliases mia
                    join master_industries mi on mi.id = mia.master_industry_id
                  ) industry_aliases;");

            var languageRows = connection.Query<LanguageAliasRow>(
                @"select
                    mla.alias,
                    ml.code as Code,
                    ml.label as Label
                  from master_language_aliases mla
                  join master_languages ml on ml.id = mla.master_language_id;");

            return BuildSnapshot(locationRows, canonicalLocationRows, industryRows, languageRows);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Lead master data could not be loaded. Ensure master locations, industries, and languages are initialized before startup.",
                ex);
        }
    }

    private static LeadMasterDataSnapshot BuildSnapshot(
        IEnumerable<LocationAliasRow> locationRows,
        IEnumerable<CanonicalLocationRow> canonicalLocationRows,
        IEnumerable<IndustryAliasRow> industryRows,
        IEnumerable<LanguageAliasRow> languageRows)
    {
        var locationAliases = locationRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Alias) && !string.IsNullOrWhiteSpace(row.CanonicalName))
            .GroupBy(row => NormalizeToken(row.Alias))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var row = group.First();
                    return new LocationAliasValue(row.CanonicalName.Trim(), row.Latitude, row.Longitude);
                },
                StringComparer.Ordinal);

        var industryAliases = industryRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Alias) && !string.IsNullOrWhiteSpace(row.Label) && !string.IsNullOrWhiteSpace(row.Code))
            .GroupBy(row => NormalizeToken(row.Alias))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var row = group.First();
                    return new IndustryAliasValue(row.Code.Trim(), row.Label.Trim());
                },
                StringComparer.Ordinal);

        var languageAliases = languageRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Alias) && !string.IsNullOrWhiteSpace(row.Label) && !string.IsNullOrWhiteSpace(row.Code))
            .GroupBy(row => NormalizeToken(row.Alias))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var row = group.First();
                    return new LanguageAliasValue(row.Code.Trim(), row.Label.Trim());
                },
                StringComparer.Ordinal);

        var canonicalLocations = canonicalLocationRows
            .Where(row => !string.IsNullOrWhiteSpace(row.CanonicalName))
            .GroupBy(row => NormalizeToken(row.CanonicalName))
            .Select(group => group.First())
            .Select(row => new CanonicalLocationValue(
                row.CanonicalName.Trim(),
                string.IsNullOrWhiteSpace(row.LocationType) ? null : row.LocationType.Trim(),
                string.IsNullOrWhiteSpace(row.ParentCity) ? null : row.ParentCity.Trim(),
                string.IsNullOrWhiteSpace(row.Province) ? null : row.Province.Trim(),
                row.Latitude,
                row.Longitude))
            .ToArray();

        if (industryAliases.Count == 0)
        {
            throw new InvalidOperationException("Lead master data is missing industry aliases.");
        }

        if (languageAliases.Count == 0)
        {
            throw new InvalidOperationException("Lead master data is missing language aliases.");
        }

        return new LeadMasterDataSnapshot(
            locationAliases,
            canonicalLocations,
            industryAliases,
            languageAliases,
            BuildTokens(locationAliases.Keys),
            BuildTokens(industryAliases.Keys),
            BuildTokens(languageAliases.Keys));
    }

    private static IReadOnlyList<string> BuildTokens(IEnumerable<string> tableTokens)
    {
        return BuildTokens(tableTokens, Array.Empty<string>());
    }

    private static IReadOnlyList<string> BuildTokens(IEnumerable<string> tableTokens, IEnumerable<string> fallbackTokens)
    {
        return tableTokens
            .Concat(fallbackTokens)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeToken(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radiusKm = 6371.0;
        static double ToRadians(double angle) => Math.PI * angle / 180.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2d), 2d);
        var c = 2d * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
        return radiusKm * c;
    }

    private sealed record LeadMasterDataSnapshot(
        IReadOnlyDictionary<string, LocationAliasValue> LocationAliases,
        IReadOnlyList<CanonicalLocationValue> CanonicalLocations,
        IReadOnlyDictionary<string, IndustryAliasValue> IndustryAliases,
        IReadOnlyDictionary<string, LanguageAliasValue> LanguageAliases,
        IReadOnlyList<string> LocationTokens,
        IReadOnlyList<string> IndustryTokens,
        IReadOnlyList<string> LanguageTokens);

    private sealed record LocationAliasValue(string CanonicalName, double? Latitude, double? Longitude);
    private sealed record CanonicalLocationValue(string CanonicalName, string? LocationType, string? ParentCity, string? Province, double? Latitude, double? Longitude);
    private sealed record IndustryAliasValue(string Code, string Label);
    private sealed record LanguageAliasValue(string Code, string Label);

    private sealed class LocationAliasRow
    {
        public string Alias { get; init; } = string.Empty;
        public string CanonicalName { get; init; } = string.Empty;
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    private sealed class IndustryAliasRow
    {
        public string Alias { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    private sealed class CanonicalLocationRow
    {
        public string CanonicalName { get; init; } = string.Empty;
        public string? LocationType { get; init; }
        public string? ParentCity { get; init; }
        public string? Province { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    private sealed class LanguageAliasRow
    {
        public string Alias { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }
}

using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class LeadMasterDataService : ILeadMasterDataService
{
    private static readonly string[] FallbackIndustryTokens = { "funeral", "retail", "clinic", "legal", "restaurant" };
    private static readonly string[] FallbackLanguageTokens = { "english", "afrikaans", "isizulu", "isixhosa", "sesotho" };

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
                    mla.alias,
                    ml.canonical_name as CanonicalName,
                    ml.latitude as Latitude,
                    ml.longitude as Longitude
                  from master_location_aliases mla
                  join master_locations ml on ml.id = mla.master_location_id;");

            var industryRows = connection.Query<IndustryAliasRow>(
                @"select
                    mia.alias,
                    mi.code as Code,
                    mi.label as Label
                  from master_industry_aliases mia
                  join master_industries mi on mi.id = mia.master_industry_id;");

            var languageRows = connection.Query<LanguageAliasRow>(
                @"select
                    mla.alias,
                    ml.code as Code,
                    ml.label as Label
                  from master_language_aliases mla
                  join master_languages ml on ml.id = mla.master_language_id;");

            return BuildSnapshot(locationRows, industryRows, languageRows);
        }
        catch
        {
            return BuildSnapshot(
                Array.Empty<LocationAliasRow>(),
                Array.Empty<IndustryAliasRow>(),
                Array.Empty<LanguageAliasRow>());
        }
    }

    private static LeadMasterDataSnapshot BuildSnapshot(
        IEnumerable<LocationAliasRow> locationRows,
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

        return new LeadMasterDataSnapshot(
            locationAliases,
            industryAliases,
            languageAliases,
            BuildTokens(locationAliases.Keys),
            BuildTokens(industryAliases.Keys, FallbackIndustryTokens),
            BuildTokens(languageAliases.Keys, FallbackLanguageTokens));
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

    private sealed record LeadMasterDataSnapshot(
        IReadOnlyDictionary<string, LocationAliasValue> LocationAliases,
        IReadOnlyDictionary<string, IndustryAliasValue> IndustryAliases,
        IReadOnlyDictionary<string, LanguageAliasValue> LanguageAliases,
        IReadOnlyList<string> LocationTokens,
        IReadOnlyList<string> IndustryTokens,
        IReadOnlyList<string> LanguageTokens);

    private sealed record LocationAliasValue(string CanonicalName, double? Latitude, double? Longitude);
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

    private sealed class LanguageAliasRow
    {
        public string Alias { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }
}

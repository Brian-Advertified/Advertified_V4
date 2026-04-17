using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class LocationCatalogService : ILocationCatalogService
{
    private readonly NpgsqlDataSource _dataSource;

    public LocationCatalogService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task SeedResolvedLocationAsync(SaveCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        var entry = BuildEntry(request);
        if (entry is null)
        {
            return;
        }

        const string sql = @"
with upserted_location as (
    insert into master_locations (
        canonical_name,
        location_type,
        parent_city,
        province,
        country,
        latitude,
        longitude,
        source_system,
        is_verified,
        last_seen_at,
        updated_at
    )
    values (
        @CanonicalName,
        @LocationType,
        @ParentCity,
        @Province,
        'South Africa',
        @Latitude,
        @Longitude,
        @SourceSystem,
        @IsVerified,
        now(),
        now()
    )
    on conflict (canonical_name) do update
    set
        location_type = excluded.location_type,
        parent_city = coalesce(excluded.parent_city, master_locations.parent_city),
        province = coalesce(excluded.province, master_locations.province),
        latitude = coalesce(excluded.latitude, master_locations.latitude),
        longitude = coalesce(excluded.longitude, master_locations.longitude),
        source_system = excluded.source_system,
        is_verified = excluded.is_verified,
        last_seen_at = now(),
        updated_at = now()
    returning id
)
insert into master_location_aliases (master_location_id, alias)
select location.id, alias_value.alias
from upserted_location location
cross join lateral (
    select unnest(@Aliases::text[]) as alias
) alias_value
where coalesce(trim(alias_value.alias), '') <> ''
on conflict (alias) do nothing;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entry.CanonicalName,
                entry.LocationType,
                entry.ParentCity,
                entry.Province,
                entry.Latitude,
                entry.Longitude,
                entry.SourceSystem,
                entry.IsVerified,
                Aliases = entry.Aliases
            },
            cancellationToken: cancellationToken));
    }

    private static CatalogLocationEntry? BuildEntry(SaveCampaignBriefRequest request)
    {
        if (!request.TargetLatitude.HasValue || !request.TargetLongitude.HasValue)
        {
            return null;
        }

        var label = Normalize(request.TargetLocationLabel);
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var city = Normalize(request.TargetLocationCity);
        var province = Normalize(request.TargetLocationProvince);
        var scope = Normalize(request.GeographyScope)?.ToLowerInvariant();
        var locationType = scope switch
        {
            "provincial" => "province",
            "national" => "country",
            "local" when !string.IsNullOrWhiteSpace(city)
                && label.Equals(city, StringComparison.OrdinalIgnoreCase) => "city",
            "local" => "suburb",
            _ => "place"
        };

        return new CatalogLocationEntry(
            label,
            locationType,
            city,
            province,
            request.TargetLatitude.Value,
            request.TargetLongitude.Value,
            "campaign_brief",
            false,
            BuildAliases(label, city));
    }

    private static string[] BuildAliases(string label, string? city)
    {
        var values = new List<string>
        {
            label
        };

        var shortLabel = label.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(shortLabel))
        {
            values.Add(shortLabel);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            values.Add(city);
        }

        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record CatalogLocationEntry(
        string CanonicalName,
        string LocationType,
        string? ParentCity,
        string? Province,
        double Latitude,
        double Longitude,
        string SourceSystem,
        bool IsVerified,
        string[] Aliases);
}

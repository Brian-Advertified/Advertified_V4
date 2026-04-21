using Advertified.App.Contracts.Public;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class PublicLocationSearchService : IPublicLocationSearchService
{
    private readonly NpgsqlDataSource _dataSource;

    public PublicLocationSearchService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<string>> ListSuburbsAsync(string city, CancellationToken cancellationToken)
    {
        var normalizedCity = city?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCity))
        {
            return Array.Empty<string>();
        }

        const string sql = @"
            select trim(oii.suburb) as suburb
            from ooh_inventory_intelligence oii
            where oii.is_active = true
              and lower(coalesce(oii.city, '')) = lower(@City)
              and coalesce(trim(oii.suburb), '') <> ''
            group by trim(oii.suburb)
            order by trim(oii.suburb)
            limit 50;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            sql,
            new { City = normalizedCity },
            cancellationToken: cancellationToken));

        return rows
            .Where(static suburb => !string.IsNullOrWhiteSpace(suburb))
            .Select(static suburb => suburb.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<PublicLocationSuggestionResponse>> SearchAsync(
        string query,
        string? geographyScope,
        string? city,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<PublicLocationSuggestionResponse>();
        }

        var scope = geographyScope?.Trim().ToLowerInvariant();
        var normalizedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        var take = Math.Clamp(maxResults, 1, 20);

        const string sql = @"
            with master_matches as (
                select
                    ml.canonical_name as label,
                    lower(ml.location_type) as location_type,
                    ml.parent_city as city,
                    ml.province as province,
                    ml.latitude as latitude,
                    ml.longitude as longitude,
                    ml.source_system as source,
                    0 as source_rank
                from master_locations ml
                where (
                    ml.canonical_name ilike @Pattern
                    or exists (
                        select 1
                        from master_location_aliases mla
                        where mla.master_location_id = ml.id
                          and mla.alias ilike @Pattern
                    )
                )
                  and (
                    @Scope <> 'local'
                    or lower(ml.location_type) in ('city', 'suburb')
                  )
            ),
            inventory_suburbs as (
                select
                    trim(oii.suburb) as label,
                    'suburb'::text as location_type,
                    nullif(trim(oii.city), '') as city,
                    nullif(trim(oii.province), '') as province,
                    avg(oii.latitude) as latitude,
                    avg(oii.longitude) as longitude,
                    'inventory'::text as source,
                    1 as source_rank
                from ooh_inventory_intelligence oii
                where oii.is_active = true
                  and coalesce(trim(oii.suburb), '') <> ''
                  and (
                    trim(oii.suburb) ilike @Pattern
                    or trim(coalesce(oii.city, '')) ilike @Pattern
                  )
                  and (
                    @Scope <> 'local'
                    or @City is null
                    or lower(coalesce(oii.city, '')) = lower(@City)
                  )
                group by trim(oii.suburb), nullif(trim(oii.city), ''), nullif(trim(oii.province), '')
            ),
            inventory_cities as (
                select
                    trim(oii.city) as label,
                    'city'::text as location_type,
                    trim(oii.city) as city,
                    nullif(trim(oii.province), '') as province,
                    avg(oii.latitude) as latitude,
                    avg(oii.longitude) as longitude,
                    'inventory'::text as source,
                    2 as source_rank
                from ooh_inventory_intelligence oii
                where oii.is_active = true
                  and coalesce(trim(oii.city), '') <> ''
                  and trim(oii.city) ilike @Pattern
                  and @Scope <> 'provincial'
                group by trim(oii.city), nullif(trim(oii.province), '')
            ),
            combined as (
                select * from master_matches
                union all
                select * from inventory_suburbs
                union all
                select * from inventory_cities
            ),
            ranked as (
                select
                    label,
                    location_type as LocationType,
                    city as City,
                    province as Province,
                    latitude as Latitude,
                    longitude as Longitude,
                    source as Source,
                    row_number() over (
                        partition by lower(label), coalesce(lower(city), ''), location_type
                        order by source_rank, label
                    ) as row_num,
                    case
                        when lower(label) = lower(@ExactQuery) then 0
                        when lower(label) like lower(@StartsWithPattern) then 1
                        when city is not null and lower(city) = lower(@ExactQuery) then 2
                        else 3
                    end as match_rank
                from combined
                where coalesce(label, '') <> ''
            )
            select
                label,
                LocationType,
                City,
                Province,
                Latitude,
                Longitude,
                Source
            from ranked
            where row_num = 1
            order by match_rank, label, city nulls last
            limit @Take;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PublicLocationSuggestionResponse>(new CommandDefinition(
            sql,
            new
            {
                Pattern = $"%{normalizedQuery}%",
                ExactQuery = normalizedQuery,
                StartsWithPattern = $"{normalizedQuery}%",
                Scope = scope ?? string.Empty,
                City = normalizedCity,
                Take = take
            },
            cancellationToken: cancellationToken));

        return rows.ToArray();
    }
}

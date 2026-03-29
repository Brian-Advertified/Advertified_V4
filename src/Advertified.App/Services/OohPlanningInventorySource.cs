using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class OohPlanningInventorySource : IOohPlanningInventorySource
{
    private readonly string _connectionString;

    public OohPlanningInventorySource(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<OohPlanningInventoryRow>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
select
    iif.id as SourceId,
    'ooh' as SourceType,
    coalesce(iif.site_name, 'OOH Site') as DisplayName,
    'OOH' as MediaType,
    iif.media_type as Subtype,
    iif.province as Province,
    iif.city as City,
    iif.suburb as Suburb,
    coalesce(nullif(iif.suburb, ''), nullif(iif.city, ''), nullif(iif.province, '')) as Area,
    coalesce(nullif(iif.metadata_json ->> 'language', ''), 'N/A') as Language,
    null::int as LsmMin,
    null::int as LsmMax,
    coalesce((iif.metadata_json ->> 'discounted_rate_zar')::numeric, (iif.metadata_json ->> 'rate_card_zar')::numeric, 0) as Cost,
    coalesce(nullif(iif.metadata_json ->> 'available', ''), 'true') <> 'false' as IsAvailable,
    false as PackageOnly,
    coalesce(nullif(iif.metadata_json ->> 'time_band', ''), nullif(iif.metadata_json ->> 'daypart', ''), 'always_on') as TimeBand,
    null as DayType,
    coalesce(nullif(iif.media_type, ''), nullif(iif.metadata_json ->> 'slot_type', ''), 'placement') as SlotType,
    nullif(iif.metadata_json ->> 'duration_seconds', '')::int as DurationSeconds,
    coalesce(rc.code, '') as RegionClusterCode,
    coalesce(nullif(iif.metadata_json ->> 'geography_scope', ''), nullif(iif.province, ''), '') as MarketScope,
    null as MarketTier,
    null::int as MonthlyListenership,
    false as IsFlagshipStation,
    false as IsPremiumStation,
    iif.metadata_json::text as MetadataJson
from inventory_items_final iif
left join region_clusters rc on rc.id = iif.region_cluster_id
where coalesce((iif.metadata_json ->> 'discounted_rate_zar')::numeric, (iif.metadata_json ->> 'rate_card_zar')::numeric, 0) <= @Budget;";

        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<OohPlanningInventoryRow>(new CommandDefinition(
            sql,
            new { Budget = request.SelectedBudget },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}


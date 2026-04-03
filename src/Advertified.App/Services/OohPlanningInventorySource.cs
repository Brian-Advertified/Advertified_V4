using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class OohPlanningInventorySource : IOohPlanningInventorySource
{
    private readonly string _connectionString;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;

    public OohPlanningInventorySource(string connectionString, IPricingSettingsProvider pricingSettingsProvider)
    {
        _connectionString = connectionString;
        _pricingSettingsProvider = pricingSettingsProvider;
    }

    public async Task<List<OohPlanningInventoryRow>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
select
    iif.id as SourceId,
    'ooh' as SourceType,
    coalesce(iif.site_name, 'Billboard or Digital Screen Site') as DisplayName,
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
where coalesce((iif.metadata_json ->> 'discounted_rate_zar')::numeric, (iif.metadata_json ->> 'rate_card_zar')::numeric, 0) <= @Budget

union all

select
    mop.id as SourceId,
    'ooh' as SourceType,
    concat(mo.name, ' - ', mop.package_name) as DisplayName,
    'OOH' as MediaType,
    coalesce(nullif(mop.package_type, ''), 'package') as Subtype,
    nullif(geo.province_code, '') as Province,
    nullif(geo.city_name, '') as City,
    null::text as Suburb,
    coalesce(nullif(geo.city_name, ''), nullif(geo.province_code, ''), nullif(mo.coverage_type, ''), 'national') as Area,
    coalesce(nullif(lang.language_display, ''), 'N/A') as Language,
    null::int as LsmMin,
    null::int as LsmMax,
    coalesce(mop.cost_per_month_zar, mop.investment_zar, mop.value_zar, 0) as Cost,
    mop.is_active as IsAvailable,
    true as PackageOnly,
    'always_on' as TimeBand,
    null::text as DayType,
    'placement' as SlotType,
    null::int as DurationSeconds,
    coalesce(nullif(geo.province_code, ''), nullif(mo.coverage_type, ''), 'national') as RegionClusterCode,
    coalesce(nullif(mo.coverage_type, ''), 'national') as MarketScope,
    coalesce(nullif(mo.catalog_health, ''), 'mixed_not_fully_healthy') as MarketTier,
    null::int as MonthlyListenership,
    false as IsFlagshipStation,
    false as IsPremiumStation,
    jsonb_build_object(
        'sourceType', 'ooh',
        'mediaType', 'OOH',
        'package_name', mop.package_name,
        'package_type', mop.package_type,
        'notes', mop.notes,
        'source_name', mop.source_name,
        'source_date', mop.source_date,
        'coverage_type', mo.coverage_type,
        'catalog_health', mo.catalog_health,
        'province', geo.province_code,
        'city', geo.city_name,
        'language', lang.language_display,
        'pricingModel', 'fixed_placement_total',
        'rateBasis', 'per_placement'
    )::text as MetadataJson
from media_outlet mo
join media_outlet_pricing_package mop on mop.media_outlet_id = mo.id
left join lateral (
    select
        mog.province_code,
        mog.city_name
    from media_outlet_geography mog
    where mog.media_outlet_id = mo.id
    order by
        case when mog.geography_type = 'city' then 0 else 1 end,
        mog.city_name nulls last,
        mog.province_code nulls last
    limit 1
) geo on true
left join lateral (
    select string_agg(mol.language_code, '/' order by mol.language_code) as language_display
    from media_outlet_language mol
    where mol.media_outlet_id = mo.id
) lang on true
where lower(mo.media_type) = 'ooh'
  and mop.is_active = true
  and coalesce(mop.source_name, '') <> 'bootstrap seed'
  and coalesce(mo.data_source_enrichment, '') <> 'bootstrap_ooh_seed_v1'
  and coalesce(mop.cost_per_month_zar, mop.investment_zar, mop.value_zar, 0) <= @Budget;";

        await using var conn = new NpgsqlConnection(_connectionString);
        var pricingSettings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);
        var rows = await conn.QueryAsync<OohPlanningInventoryRow>(new CommandDefinition(
            sql,
            new { Budget = request.SelectedBudget },
            cancellationToken: cancellationToken));

        return rows
            .Select(row =>
            {
                row.Subtype = OohInventoryNormalizer.NormalizeSubtype(
                    row.Subtype,
                    row.SlotType,
                    row.DisplayName,
                    row.City,
                    row.Suburb,
                    row.Province);
                row.SlotType = OohInventoryNormalizer.NormalizeSlotType(row.SlotType, row.Subtype);

                var rawCost = row.Cost;
                row.Cost = PricingPolicy.ApplyMarkup(rawCost, row.MediaType, row.Subtype, pricingSettings);
                return row;
            })
            .Where(row => row.Cost > 0m && row.Cost <= request.SelectedBudget)
            .ToList();
    }
}

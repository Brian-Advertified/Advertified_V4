using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class OohPlanningInventorySource : IOohPlanningInventorySource
{
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;

    public OohPlanningInventorySource(Npgsql.NpgsqlDataSource dataSource, IPricingSettingsProvider pricingSettingsProvider)
    {
        _dataSource = dataSource;
        _pricingSettingsProvider = pricingSettingsProvider;
    }

    public async Task<List<OohPlanningInventoryRow>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
with ooh_intelligence as (
    select *
    from ooh_inventory_intelligence
    where is_active = true
)
select
    iif.id as SourceId,
    'ooh' as SourceType,
    coalesce(nullif(oii.site_name, ''), iif.site_name, 'Billboard or Digital Screen Site') as DisplayName,
    'OOH' as MediaType,
    iif.media_type as Subtype,
    iif.province as Province,
    iif.city as City,
    iif.suburb as Suburb,
    coalesce(nullif(iif.suburb, ''), nullif(iif.city, ''), nullif(iif.province, '')) as Area,
    coalesce(nullif(iif.metadata_json ->> 'language', ''), 'N/A') as Language,
    coalesce(
        nullif(split_part(regexp_replace(coalesce(oii.audience_income_fit, ''), '[^0-9]+', ' ', 'g'), ' ', 1), '')::int,
        null
    ) as LsmMin,
    coalesce(
        nullif(split_part(regexp_replace(coalesce(oii.audience_income_fit, ''), '[^0-9]+', ' ', 'g'), ' ', 2), '')::int,
        nullif(split_part(regexp_replace(coalesce(oii.audience_income_fit, ''), '[^0-9]+', ' ', 'g'), ' ', 1), '')::int
    ) as LsmMax,
    coalesce(
        (iif.metadata_json ->> 'discounted_rate_zar')::numeric,
        (iif.metadata_json ->> 'rate_card_zar')::numeric,
        (iif.metadata_json ->> 'monthly_rate_zar')::numeric,
        0) as Cost,
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
    coalesce(iif.latitude, nullif(iif.metadata_json ->> 'latitude', '')::double precision, nullif(iif.metadata_json ->> 'lat', '')::double precision) as Latitude,
    coalesce(iif.longitude, nullif(iif.metadata_json ->> 'longitude', '')::double precision, nullif(iif.metadata_json ->> 'lng', '')::double precision, nullif(iif.metadata_json ->> 'lon', '')::double precision) as Longitude,
    jsonb_strip_nulls(
        coalesce(iif.metadata_json, '{}'::jsonb) ||
        jsonb_build_object(
            'inventoryIntelligenceNotes', oii.intelligence_notes,
            'inventory_intelligence_notes', oii.intelligence_notes,
            'venueType', oii.venue_type,
            'venue_type', oii.venue_type,
            'premiumMassFit', oii.premium_mass_fit,
            'premium_mass_fit', oii.premium_mass_fit,
            'pricePositioningFit', oii.price_positioning_fit,
            'price_positioning_fit', oii.price_positioning_fit,
            'audienceIncomeFit', oii.audience_income_fit,
            'audience_income_fit', oii.audience_income_fit,
            'youthFit', oii.youth_fit,
            'youth_fit', oii.youth_fit,
            'familyFit', oii.family_fit,
            'family_fit', oii.family_fit,
            'professionalFit', oii.professional_fit,
            'professional_fit', oii.professional_fit,
            'commuterFit', oii.commuter_fit,
            'commuter_fit', oii.commuter_fit,
            'touristFit', oii.tourist_fit,
            'tourist_fit', oii.tourist_fit,
            'highValueShopperFit', oii.high_value_shopper_fit,
            'high_value_shopper_fit', oii.high_value_shopper_fit,
            'audienceAgeSkew', oii.audience_age_skew,
            'audience_age_skew', oii.audience_age_skew,
            'audienceGenderSkew', oii.audience_gender_skew,
            'audience_gender_skew', oii.audience_gender_skew,
            'dwellTimeScore', oii.dwell_time_score,
            'dwell_time_score', oii.dwell_time_score,
            'environmentType', oii.environment_type,
            'environment_type', oii.environment_type,
            'buyingBehaviourFit', oii.buying_behaviour_fit,
            'buying_behaviour_fit', oii.buying_behaviour_fit,
            'dataConfidence', oii.data_confidence,
            'data_confidence', oii.data_confidence,
            'primaryAudienceTags', oii.primary_audience_tags_json,
            'primary_audience_tags', oii.primary_audience_tags_json,
            'secondaryAudienceTags', oii.secondary_audience_tags_json,
            'secondary_audience_tags', oii.secondary_audience_tags_json,
            'recommendationTags', oii.recommendation_tags_json,
            'recommendation_tags', oii.recommendation_tags_json
        ) ||
        coalesce(oii.metadata_json, '{}'::jsonb)
    )::text as MetadataJson
from inventory_items_final iif
left join region_clusters rc on rc.id = iif.region_cluster_id
left join lateral (
    select intelligence.*
    from ooh_intelligence intelligence
    where lower(coalesce(intelligence.supplier, '')) = lower(coalesce(iif.supplier, ''))
      and (
          coalesce(nullif(lower(coalesce(intelligence.site_code, '')), ''), '__none__')
          = coalesce(nullif(lower(coalesce(iif.metadata_json ->> 'site_code', '')), ''), '__none__')
      )
      and regexp_replace(lower(coalesce(intelligence.site_name, '')), '[^a-z0-9]+', '', 'g')
          = regexp_replace(lower(regexp_replace(coalesce(iif.site_name, ''), ',,', ',', 'g')), '[^a-z0-9]+', '', 'g')
      and regexp_replace(lower(coalesce(intelligence.city, '')), '[^a-z0-9]+', '', 'g')
          = regexp_replace(lower(coalesce(iif.city, '')), '[^a-z0-9]+', '', 'g')
      and regexp_replace(lower(coalesce(intelligence.suburb, '')), '[^a-z0-9]+', '', 'g')
          = regexp_replace(lower(coalesce(iif.suburb, '')), '[^a-z0-9]+', '', 'g')
      and regexp_replace(lower(coalesce(intelligence.province, '')), '[^a-z0-9]+', '', 'g')
          = regexp_replace(lower(coalesce(iif.province, '')), '[^a-z0-9]+', '', 'g')
    order by intelligence.updated_at desc, intelligence.created_at desc
    limit 1
) oii on true
where coalesce(
        (iif.metadata_json ->> 'discounted_rate_zar')::numeric,
        (iif.metadata_json ->> 'rate_card_zar')::numeric,
        (iif.metadata_json ->> 'monthly_rate_zar')::numeric,
        0) <= @Budget

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
    mo.latitude as Latitude,
    mo.longitude as Longitude,
    jsonb_strip_nulls(jsonb_build_object(
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
        'targetAudience', mo.target_audience,
        'target_audience', mo.target_audience,
        'audienceAgeSkew', mo.audience_age_skew,
        'audience_age_skew', mo.audience_age_skew,
        'audienceGenderSkew', mo.audience_gender_skew,
        'audience_gender_skew', mo.audience_gender_skew,
        'audienceLsmRange', mo.audience_lsm_range,
        'audience_lsm_range', mo.audience_lsm_range,
        'audienceRacialSkew', mo.audience_racial_skew,
        'audience_racial_skew', mo.audience_racial_skew,
        'urbanRuralMix', mo.audience_urban_rural,
        'urban_rural_mix', mo.audience_urban_rural,
        'audienceKeywords', string_to_array(coalesce(keywords.keyword_list, ''), ' | '),
        'audience_keywords', string_to_array(coalesce(keywords.keyword_list, ''), ' | '),
        'buyingBehaviourFit', mo.strategy_fit_json ->> 'buying_behaviour_fit',
        'buying_behaviour_fit', mo.strategy_fit_json ->> 'buying_behaviour_fit',
        'pricePositioningFit', mo.strategy_fit_json ->> 'price_positioning_fit',
        'price_positioning_fit', mo.strategy_fit_json ->> 'price_positioning_fit',
        'salesModelFit', mo.strategy_fit_json ->> 'sales_model_fit',
        'sales_model_fit', mo.strategy_fit_json ->> 'sales_model_fit',
        'objectiveFitPrimary', mo.strategy_fit_json ->> 'objective_fit_primary',
        'objective_fit_primary', mo.strategy_fit_json ->> 'objective_fit_primary',
        'objectiveFitSecondary', mo.strategy_fit_json ->> 'objective_fit_secondary',
        'objective_fit_secondary', mo.strategy_fit_json ->> 'objective_fit_secondary',
        'environmentType', mo.strategy_fit_json ->> 'environment_type',
        'environment_type', mo.strategy_fit_json ->> 'environment_type',
        'premiumMassFit', mo.strategy_fit_json ->> 'premium_mass_fit',
        'premium_mass_fit', mo.strategy_fit_json ->> 'premium_mass_fit',
        'dataConfidence', mo.strategy_fit_json ->> 'data_confidence',
        'data_confidence', mo.strategy_fit_json ->> 'data_confidence',
        'latitude', mo.latitude,
        'longitude', mo.longitude,
        'inventoryIntelligenceNotes', mo.strategy_fit_json ->> 'intelligence_notes',
        'inventory_intelligence_notes', mo.strategy_fit_json ->> 'intelligence_notes',
        'pricingModel', 'fixed_placement_total',
        'rateBasis', 'per_placement'
    ))::text as MetadataJson
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
left join lateral (
    select string_agg(mok.keyword, ' | ' order by mok.keyword) as keyword_list
    from media_outlet_keyword mok
    where mok.media_outlet_id = mo.id
) keywords on true
where lower(mo.media_type) = 'ooh'
  and mop.is_active = true
  and coalesce(mop.source_name, '') <> 'bootstrap seed'
  and coalesce(mo.data_source_enrichment, '') <> 'bootstrap_ooh_seed_v1'
  and coalesce(mop.cost_per_month_zar, mop.investment_zar, mop.value_zar, 0) <= @Budget;";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
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
                row.MediaType = PlanningChannelSupport.ClassifyOohChannel(row.Subtype, row.SlotType, row.DisplayName);

                var rawCost = row.Cost;
                row.Cost = PricingPolicy.ApplyMarkup(rawCost, row.MediaType, row.Subtype, pricingSettings);
                return row;
            })
            .Where(row => row.Cost > 0m && row.Cost <= request.SelectedBudget)
            .ToList();
    }
}

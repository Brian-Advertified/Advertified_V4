\if :{?csv_path}
\else
\echo 'Missing required psql variable: csv_path'
\echo 'Example: psql -v ON_ERROR_STOP=1 -v csv_path=/path/to/inventory_intelligence_enriched.csv -f tools/import_inventory_intelligence.sql'
\quit 1
\endif

BEGIN;

CREATE TEMP TABLE staging_inventory_intelligence_import (
    inventory_key text,
    channel text,
    inventory_row_type text,
    source_table text,
    source_id uuid,
    source_type text,
    outlet_code text,
    outlet_name text,
    inventory_name text,
    media_subtype text,
    package_name text,
    slot_label text,
    day_group text,
    start_time text,
    end_time text,
    province text,
    city text,
    suburb text,
    area text,
    region_cluster_code text,
    market_scope text,
    coverage_type text,
    market_tier text,
    language_display text,
    language_notes text,
    broadcast_frequency text,
    duration_seconds text,
    rate_type text,
    cost_zar text,
    raw_rate_zar text,
    investment_zar text,
    cost_per_month_zar text,
    value_zar text,
    exposure_count text,
    monthly_exposure_count text,
    duration_weeks text,
    duration_months text,
    is_available text,
    package_only text,
    is_national text,
    has_pricing text,
    listenership_daily text,
    listenership_weekly text,
    listenership_period text,
    existing_target_audience text,
    existing_audience_age_skew text,
    existing_audience_gender_skew text,
    existing_audience_lsm_range text,
    existing_audience_racial_skew text,
    existing_urban_rural_mix text,
    existing_audience_keywords text,
    existing_source_notes text,
    enrich_target_audience text,
    enrich_audience_age_skew text,
    enrich_audience_gender_skew text,
    enrich_audience_lsm_range text,
    enrich_audience_racial_skew text,
    enrich_urban_rural_mix text,
    enrich_audience_keywords text,
    enrich_buying_behaviour_fit text,
    enrich_price_positioning_fit text,
    enrich_sales_model_fit text,
    enrich_objective_fit_primary text,
    enrich_objective_fit_secondary text,
    enrich_environment_type text,
    enrich_premium_mass_fit text,
    enrich_data_confidence text,
    enrich_notes text
);

CREATE TEMP TABLE staging_inventory_outlet_keywords (
    media_outlet_id uuid not null,
    keyword text not null
);

\copy staging_inventory_intelligence_import FROM :csv_path WITH (FORMAT csv, HEADER true)

INSERT INTO staging_inventory_outlet_keywords (media_outlet_id, keyword)
SELECT
    mo.id,
    trim(token.keyword) as keyword
FROM staging_inventory_intelligence_import sii
JOIN media_outlet mo
    ON mo.code = sii.outlet_code
CROSS JOIN LATERAL regexp_split_to_table(coalesce(sii.enrich_audience_keywords, ''), '\s*\|\s*') as token(keyword)
WHERE sii.source_table = 'media_outlet_pricing_package'
  AND trim(token.keyword) <> ''
UNION
SELECT
    mo.id,
    trim(token.keyword) as keyword
FROM staging_inventory_intelligence_import sii
JOIN media_outlet_slot_rate msr
    ON msr.id = sii.source_id
JOIN media_outlet mo
    ON mo.id = msr.media_outlet_id
CROSS JOIN LATERAL regexp_split_to_table(coalesce(sii.enrich_audience_keywords, ''), '\s*\|\s*') as token(keyword)
WHERE sii.source_table = 'media_outlet_slot_rate'
  AND trim(token.keyword) <> '';

UPDATE inventory_items_final iif
SET metadata_json = coalesce(iif.metadata_json, '{}'::jsonb)
    || jsonb_strip_nulls(
        jsonb_build_object(
            'targetAudience', nullif(sii.enrich_target_audience, ''),
            'target_audience', nullif(sii.enrich_target_audience, ''),
            'audienceAgeSkew', nullif(sii.enrich_audience_age_skew, ''),
            'audience_age_skew', nullif(sii.enrich_audience_age_skew, ''),
            'audienceGenderSkew', nullif(sii.enrich_audience_gender_skew, ''),
            'audience_gender_skew', nullif(sii.enrich_audience_gender_skew, ''),
            'audienceLsmRange', nullif(sii.enrich_audience_lsm_range, ''),
            'audience_lsm_range', nullif(sii.enrich_audience_lsm_range, ''),
            'audienceRacialSkew', nullif(sii.enrich_audience_racial_skew, ''),
            'audience_racial_skew', nullif(sii.enrich_audience_racial_skew, ''),
            'urbanRuralMix', nullif(sii.enrich_urban_rural_mix, ''),
            'urban_rural_mix', nullif(sii.enrich_urban_rural_mix, ''),
            'audienceKeywords', to_jsonb(regexp_split_to_array(coalesce(nullif(sii.enrich_audience_keywords, ''), ''), '\s*\|\s*')),
            'audience_keywords', to_jsonb(regexp_split_to_array(coalesce(nullif(sii.enrich_audience_keywords, ''), ''), '\s*\|\s*')),
            'buyingBehaviourFit', nullif(sii.enrich_buying_behaviour_fit, ''),
            'buying_behaviour_fit', nullif(sii.enrich_buying_behaviour_fit, ''),
            'pricePositioningFit', nullif(sii.enrich_price_positioning_fit, ''),
            'price_positioning_fit', nullif(sii.enrich_price_positioning_fit, ''),
            'salesModelFit', nullif(sii.enrich_sales_model_fit, ''),
            'sales_model_fit', nullif(sii.enrich_sales_model_fit, ''),
            'objectiveFitPrimary', nullif(sii.enrich_objective_fit_primary, ''),
            'objective_fit_primary', nullif(sii.enrich_objective_fit_primary, ''),
            'objectiveFitSecondary', nullif(sii.enrich_objective_fit_secondary, ''),
            'objective_fit_secondary', nullif(sii.enrich_objective_fit_secondary, ''),
            'environmentType', nullif(sii.enrich_environment_type, ''),
            'environment_type', nullif(sii.enrich_environment_type, ''),
            'premiumMassFit', nullif(sii.enrich_premium_mass_fit, ''),
            'premium_mass_fit', nullif(sii.enrich_premium_mass_fit, ''),
            'dataConfidence', nullif(sii.enrich_data_confidence, ''),
            'data_confidence', nullif(sii.enrich_data_confidence, ''),
            'inventoryIntelligenceNotes', nullif(sii.enrich_notes, ''),
            'inventory_intelligence_notes', nullif(sii.enrich_notes, '')
        )
    )
FROM staging_inventory_intelligence_import sii
WHERE sii.source_table = 'inventory_items_final'
  AND iif.id = sii.source_id;

UPDATE media_outlet mo
SET target_audience = nullif(outlet_data.enrich_target_audience, ''),
    audience_age_skew = nullif(outlet_data.enrich_audience_age_skew, ''),
    audience_gender_skew = nullif(outlet_data.enrich_audience_gender_skew, ''),
    audience_lsm_range = nullif(outlet_data.enrich_audience_lsm_range, ''),
    audience_racial_skew = nullif(outlet_data.enrich_audience_racial_skew, ''),
    audience_urban_rural = nullif(outlet_data.enrich_urban_rural_mix, ''),
    strategy_fit_json = jsonb_strip_nulls(
        jsonb_build_object(
            'buying_behaviour_fit', nullif(outlet_data.enrich_buying_behaviour_fit, ''),
            'price_positioning_fit', nullif(outlet_data.enrich_price_positioning_fit, ''),
            'sales_model_fit', nullif(outlet_data.enrich_sales_model_fit, ''),
            'objective_fit_primary', nullif(outlet_data.enrich_objective_fit_primary, ''),
            'objective_fit_secondary', nullif(outlet_data.enrich_objective_fit_secondary, ''),
            'environment_type', nullif(outlet_data.enrich_environment_type, ''),
            'premium_mass_fit', nullif(outlet_data.enrich_premium_mass_fit, ''),
            'data_confidence', nullif(outlet_data.enrich_data_confidence, ''),
            'intelligence_notes', nullif(outlet_data.enrich_notes, '')
        )
    ),
    updated_at = now()
FROM (
    SELECT DISTINCT ON (media_outlet_id)
        media_outlet_id,
        enrich_target_audience,
        enrich_audience_age_skew,
        enrich_audience_gender_skew,
        enrich_audience_lsm_range,
        enrich_audience_racial_skew,
        enrich_urban_rural_mix,
        enrich_buying_behaviour_fit,
        enrich_price_positioning_fit,
        enrich_sales_model_fit,
        enrich_objective_fit_primary,
        enrich_objective_fit_secondary,
        enrich_environment_type,
        enrich_premium_mass_fit,
        enrich_data_confidence,
        enrich_notes
    FROM (
        SELECT
            mo.id as media_outlet_id,
            sii.enrich_target_audience,
            sii.enrich_audience_age_skew,
            sii.enrich_audience_gender_skew,
            sii.enrich_audience_lsm_range,
            sii.enrich_audience_racial_skew,
            sii.enrich_urban_rural_mix,
            sii.enrich_buying_behaviour_fit,
            sii.enrich_price_positioning_fit,
            sii.enrich_sales_model_fit,
            sii.enrich_objective_fit_primary,
            sii.enrich_objective_fit_secondary,
            sii.enrich_environment_type,
            sii.enrich_premium_mass_fit,
            sii.enrich_data_confidence,
            sii.enrich_notes,
            sii.inventory_row_type
        FROM staging_inventory_intelligence_import sii
        JOIN media_outlet mo
            ON mo.code = sii.outlet_code
        WHERE sii.source_table = 'media_outlet_pricing_package'

        UNION ALL

        SELECT
            mo.id as media_outlet_id,
            sii.enrich_target_audience,
            sii.enrich_audience_age_skew,
            sii.enrich_audience_gender_skew,
            sii.enrich_audience_lsm_range,
            sii.enrich_audience_racial_skew,
            sii.enrich_urban_rural_mix,
            sii.enrich_buying_behaviour_fit,
            sii.enrich_price_positioning_fit,
            sii.enrich_sales_model_fit,
            sii.enrich_objective_fit_primary,
            sii.enrich_objective_fit_secondary,
            sii.enrich_environment_type,
            sii.enrich_premium_mass_fit,
            sii.enrich_data_confidence,
            sii.enrich_notes,
            sii.inventory_row_type
        FROM staging_inventory_intelligence_import sii
        JOIN media_outlet_slot_rate msr
            ON msr.id = sii.source_id
        JOIN media_outlet mo
            ON mo.id = msr.media_outlet_id
        WHERE sii.source_table = 'media_outlet_slot_rate'
    ) outlet_rows
    ORDER BY media_outlet_id, case when inventory_row_type = 'package' then 0 else 1 end
) outlet_data
WHERE mo.id = outlet_data.media_outlet_id;

DELETE FROM media_outlet_keyword mok
USING (
    SELECT DISTINCT media_outlet_id
    FROM staging_inventory_outlet_keywords
) touched
WHERE mok.media_outlet_id = touched.media_outlet_id;

INSERT INTO media_outlet_keyword (id, media_outlet_id, keyword)
SELECT
    gen_random_uuid(),
    media_outlet_id,
    keyword
FROM (
    SELECT DISTINCT media_outlet_id, keyword
    FROM staging_inventory_outlet_keywords
) keywords;

COMMIT;

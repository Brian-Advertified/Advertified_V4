CREATE OR REPLACE FUNCTION normalize_station_name(input TEXT)
RETURNS TEXT
LANGUAGE SQL
IMMUTABLE
AS $$
    SELECT lower(trim(regexp_replace(coalesce(input, ''), '[^a-zA-Z0-9]+', ' ', 'g')));
$$;

CREATE OR REPLACE FUNCTION parse_zar_text(input TEXT)
RETURNS NUMERIC
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    cleaned TEXT;
BEGIN
    IF input IS NULL OR btrim(input) = '' THEN
        RETURN NULL;
    END IF;

    cleaned := regexp_replace(input, '[^0-9\.]+', '', 'g');

    IF cleaned = '' THEN
        RETURN NULL;
    END IF;

    RETURN cleaned::numeric;
END;
$$;

BEGIN;

TRUNCATE TABLE radio_packages_final RESTART IDENTITY CASCADE;
TRUNCATE TABLE radio_slots_final RESTART IDENTITY CASCADE;
TRUNCATE TABLE inventory_items_final RESTART IDENTITY CASCADE;
TRUNCATE TABLE radio_stations RESTART IDENTITY CASCADE;

INSERT INTO radio_stations (name, normalized_name)
SELECT DISTINCT station_name, normalize_station_name(station_name)
FROM (
    SELECT NULLIF(trim(both ' -' FROM supplier_or_station), '') AS station_name
    FROM radio_packages
    UNION
    SELECT NULLIF(trim(station), '') AS station_name
    FROM radio_slot_grids
    UNION
    SELECT NULLIF(trim(product_name), '') AS station_name
    FROM sabc_rate_tables
    WHERE channel_type ILIKE '%radio%'
) s
WHERE station_name IS NOT NULL
  AND station_name <> ''
ON CONFLICT (normalized_name) DO NOTHING;

INSERT INTO radio_packages_final (
    station_id,
    name,
    total_cost,
    metadata_json
)
SELECT
    rs.id,
    coalesce(nullif(trim(rp.element_name), ''), 'Unnamed package element'),
    parse_zar_text(rp.investment_zar),
    jsonb_build_object(
        'source_file', rp.source_file,
        'channel', rp.channel,
        'supplier_or_station', rp.supplier_or_station,
        'exposure', rp.exposure,
        'value_zar', rp.value_zar,
        'saving_or_discount_zar', rp.saving_or_discount_zar,
        'duration', rp.duration,
        'notes', rp.notes
    )
FROM radio_packages rp
JOIN radio_stations rs
  ON rs.normalized_name = normalize_station_name(trim(both ' -' FROM rp.supplier_or_station))
WHERE coalesce(trim(rp.element_name), '') <> '';

INSERT INTO radio_slots_final (
    station_id,
    source_kind,
    time_band,
    day_type,
    slot_type,
    duration_seconds,
    rate,
    metadata_json
)
SELECT
    rs.id,
    'radio_slot_grid',
    slot.time_band,
    slot.day_type,
    'commercial',
    rsg.ad_length_seconds,
    coalesce(
        rsg.avg_cost_per_spot_zar,
        case
            when rsg.spots_count is not null and rsg.spots_count > 0 and coalesce(rsg.package_cost_zar, rsg.total_invoice_zar) is not null
                then round(coalesce(rsg.package_cost_zar, rsg.total_invoice_zar) / rsg.spots_count, 2)
            else null
        end,
        pkg.avg_rate_per_spot
    ),
    jsonb_build_object(
        'source_file', rsg.source_file,
        'package_name', rsg.package_name,
        'exposure_per_month_text', rsg.exposure_per_month_text,
        'spots_count', rsg.spots_count,
        'total_invoice_zar', rsg.total_invoice_zar,
        'package_cost_zar', rsg.package_cost_zar,
        'avg_cost_per_spot_zar', rsg.avg_cost_per_spot_zar,
        'derived_avg_rate_per_spot_zar', pkg.avg_rate_per_spot,
        'live_reads_allowed', rsg.live_reads_allowed,
        'terms_excerpt', rsg.terms_excerpt,
        'notes', rsg.notes,
        'window', slot.window_text,
        'raw_grid_excerpt', rsg.raw_grid_excerpt
    )
FROM radio_slot_grids rsg
JOIN radio_stations rs
  ON rs.normalized_name = normalize_station_name(rsg.station)
LEFT JOIN LATERAL (
    SELECT round(avg(parse_zar_text(rp.investment_zar) / nullif(regexp_replace(coalesce(rp.exposure, ''), '[^0-9]', '', 'g')::numeric, 0)), 2) AS avg_rate_per_spot
    FROM radio_packages rp
    WHERE lower(rp.source_file) = lower(rsg.source_file)
      AND parse_zar_text(rp.investment_zar) IS NOT NULL
      AND regexp_replace(coalesce(rp.exposure, ''), '[^0-9]', '', 'g') <> ''
) pkg ON TRUE
CROSS JOIN LATERAL (
    VALUES
        ('weekday', 'mon_fri', nullif(trim(rsg.monday_friday_windows), '')),
        ('saturday', 'saturday', nullif(trim(rsg.saturday_windows), '')),
        ('sunday', 'sunday', nullif(trim(rsg.sunday_windows), ''))
) AS slot(day_type, time_band, window_text)
WHERE slot.window_text IS NOT NULL;

INSERT INTO radio_slots_final (
    station_id,
    source_kind,
    time_band,
    day_type,
    slot_type,
    duration_seconds,
    rate,
    metadata_json
)
SELECT
    rs.id,
    'sabc_rate_table',
    'rate_book',
    NULL,
    'rate_card',
    NULL,
    coalesce(srt.avg_cost_per_spot_zar,
             CASE
                 WHEN srt.spots_count IS NOT NULL AND srt.spots_count > 0 AND srt.package_cost_zar IS NOT NULL
                     THEN round(srt.package_cost_zar / srt.spots_count, 2)
                 ELSE srt.package_cost_zar
             END),
    jsonb_build_object(
        'source_file', srt.source_file,
        'channel_type', srt.channel_type,
        'product_name', srt.product_name,
        'package_cost_zar', srt.package_cost_zar,
        'spots_count', srt.spots_count,
        'avg_cost_per_spot_zar', srt.avg_cost_per_spot_zar,
        'exposure_value_zar', srt.exposure_value_zar,
        'audience_segment', srt.audience_segment,
        'date_range_text', srt.date_range_text,
        'notes', srt.notes,
        'raw_excerpt', srt.raw_excerpt
    )
FROM sabc_rate_tables srt
JOIN radio_stations rs
  ON rs.normalized_name = normalize_station_name(srt.product_name)
WHERE srt.channel_type ILIKE '%radio%';

INSERT INTO inventory_items_final (
    supplier,
    media_type,
    site_name,
    city,
    suburb,
    province,
    address,
    metadata_json
)
SELECT
    split_part(ii.source_file, ' ', 1) AS supplier,
    ii.media_format,
    ii.site_title,
    coalesce(nullif(ii.city_town, ''), nullif(split_part(ii.city_province, '|', 1), '')) AS city,
    nullif(ii.suburb, ''),
    nullif(trim(split_part(coalesce(ii.city_province, ''), '|', 2)), '') AS province,
    ii.address,
    jsonb_build_object(
        'source_file', ii.source_file,
        'page', ii.page,
        'site_number', ii.site_number,
        'site_description', ii.site_description,
        'rate_card_zar', parse_zar_text(ii.rate_card_zar),
        'discounted_rate_zar', parse_zar_text(ii.discounted_rate_zar),
        'production_flighting_zar', parse_zar_text(ii.production_flighting_zar),
        'material', ii.material,
        'illuminated', ii.illuminated,
        'lsm', ii.lsm,
        'available', ii.available,
        'traffic_count', ii.traffic_count,
        'gps_coordinates', ii.gps_coordinates,
        'dimensions', ii.dimensions,
        'kmz_file', ii.kmz_file
    )
FROM inventory_items ii;

COMMIT;

create or replace function try_parse_ooh_gps_coordinate_pair(raw_value text)
returns table(latitude double precision, longitude double precision)
language plpgsql
as $$
declare
    normalized text;
    decimal_match text[];
    dms_matches text[][];
    lat_value double precision;
    lon_value double precision;
begin
    if raw_value is null or btrim(raw_value) = '' then
        return;
    end if;

    normalized := replace(replace(replace(replace(replace(replace(btrim(raw_value),
        'Notes:', ''),
        'Animation:', ''),
        'â€', '"'),
        'â€œ', '"'),
        'â€™', ''''),
        'â€˜', '''');

    decimal_match := regexp_match(normalized, '(-?\d{1,2}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)', 'i');
    if decimal_match is not null then
        lat_value := decimal_match[1]::double precision;
        lon_value := decimal_match[2]::double precision;
        if lat_value between -90 and 90 and lon_value between -180 and 180 then
            latitude := lat_value;
            longitude := lon_value;
            return next;
            return;
        end if;
    end if;

    select array_agg(match)
    into dms_matches
    from regexp_matches(normalized, '(\d{1,3})[^0-9A-Z]+(\d{1,2})[^0-9A-Z]+(\d{1,2}(?:\.\d+)?)\D*([NSEW])', 'ig') as match;

    if dms_matches is null or array_length(dms_matches, 1) < 2 then
        return;
    end if;

    lat_value := dms_matches[1][1]::double precision
        + (dms_matches[1][2]::double precision / 60.0)
        + (dms_matches[1][3]::double precision / 3600.0);
    if upper(dms_matches[1][4]) in ('S', 'W') then
        lat_value := lat_value * -1.0;
    end if;

    lon_value := dms_matches[2][1]::double precision
        + (dms_matches[2][2]::double precision / 60.0)
        + (dms_matches[2][3]::double precision / 3600.0);
    if upper(dms_matches[2][4]) in ('S', 'W') then
        lon_value := lon_value * -1.0;
    end if;

    if lat_value between -90 and 90 and lon_value between -180 and 180 then
        latitude := lat_value;
        longitude := lon_value;
        return next;
    end if;
end
$$;

with parsed_coordinates as (
    select
        iif.id,
        parsed.latitude,
        parsed.longitude
    from inventory_items_final iif
    cross join lateral try_parse_ooh_gps_coordinate_pair(coalesce(iif.metadata_json ->> 'gps_coordinates', '')) parsed
    where (iif.latitude is null or iif.longitude is null)
      and coalesce(iif.metadata_json ->> 'gps_coordinates', '') <> ''
)
update inventory_items_final iif
set latitude = coalesce(iif.latitude, parsed.latitude),
    longitude = coalesce(iif.longitude, parsed.longitude),
    metadata_json = jsonb_strip_nulls(coalesce(iif.metadata_json, '{}'::jsonb)
        || jsonb_build_object(
            'latitude', coalesce(iif.latitude, parsed.latitude),
            'longitude', coalesce(iif.longitude, parsed.longitude)))
from parsed_coordinates parsed
where parsed.id = iif.id;

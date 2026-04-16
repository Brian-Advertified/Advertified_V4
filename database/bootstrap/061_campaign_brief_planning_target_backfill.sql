with location_lookup as (
    select
        lower(btrim(ml.canonical_name)) as lookup_key,
        ml.canonical_name,
        ml.location_type,
        ml.province,
        ml.latitude,
        ml.longitude
    from master_locations ml
    union
    select
        lower(btrim(mla.alias)) as lookup_key,
        ml.canonical_name,
        ml.location_type,
        ml.province,
        ml.latitude,
        ml.longitude
    from master_location_aliases mla
    join master_locations ml on ml.id = mla.master_location_id
),
derived_target as (
    select
        cb.id,
        nullif(btrim(cb.target_location_label), '') as explicit_label,
        nullif(btrim(cb.target_location_city), '') as explicit_city,
        nullif(btrim(cb.target_location_province), '') as explicit_province,
        (
            select nullif(btrim(value), '')
            from jsonb_array_elements_text(coalesce(cb.suburbs_json::jsonb, '[]'::jsonb)) with ordinality as suburb(value, ord)
            where nullif(btrim(value), '') is not null
            order by ord
            limit 1
        ) as first_suburb,
        (
            select nullif(btrim(value), '')
            from jsonb_array_elements_text(coalesce(cb.cities_json::jsonb, '[]'::jsonb)) with ordinality as city(value, ord)
            where nullif(btrim(value), '') is not null
            order by ord
            limit 1
        ) as first_city,
        (
            select nullif(btrim(value), '')
            from jsonb_array_elements_text(coalesce(cb.areas_json::jsonb, '[]'::jsonb)) with ordinality as area(value, ord)
            where nullif(btrim(value), '') is not null
            order by ord
            limit 1
        ) as first_area,
        (
            select nullif(btrim(value), '')
            from jsonb_array_elements_text(coalesce(cb.provinces_json::jsonb, '[]'::jsonb)) with ordinality as province(value, ord)
            where nullif(btrim(value), '') is not null
            order by ord
            limit 1
        ) as first_province,
        cb.geography_scope
    from campaign_briefs cb
),
resolved_target as (
    select
        target.id,
        coalesce(
            target.explicit_label,
            lookup.canonical_name,
            target.first_suburb,
            target.first_city,
            target.first_area,
            target.first_province,
            case when lower(coalesce(target.geography_scope, '')) = 'national' then 'South Africa' end
        ) as target_label,
        coalesce(
            target.explicit_city,
            target.first_city,
            case when lookup.location_type = 'city' then lookup.canonical_name end
        ) as target_city,
        coalesce(
            target.explicit_province,
            target.first_province,
            lookup.province
        ) as target_province,
        lookup.latitude,
        lookup.longitude
    from derived_target target
    left join location_lookup lookup
        on lookup.lookup_key = lower(btrim(coalesce(
            target.explicit_label,
            target.first_suburb,
            target.first_city,
            target.first_area,
            target.first_province,
            case when lower(coalesce(target.geography_scope, '')) = 'national' then 'South Africa' end
        )))
)
update campaign_briefs cb
set
    target_location_label = coalesce(nullif(btrim(cb.target_location_label), ''), resolved.target_label),
    target_location_city = coalesce(nullif(btrim(cb.target_location_city), ''), resolved.target_city),
    target_location_province = coalesce(nullif(btrim(cb.target_location_province), ''), resolved.target_province),
    target_latitude = coalesce(cb.target_latitude, resolved.latitude),
    target_longitude = coalesce(cb.target_longitude, resolved.longitude),
    updated_at = now()
from resolved_target resolved
where cb.id = resolved.id
  and (
      (nullif(btrim(cb.target_location_label), '') is null and resolved.target_label is not null)
      or (nullif(btrim(cb.target_location_city), '') is null and resolved.target_city is not null)
      or (nullif(btrim(cb.target_location_province), '') is null and resolved.target_province is not null)
      or (cb.target_latitude is null and resolved.latitude is not null)
      or (cb.target_longitude is null and resolved.longitude is not null)
  );

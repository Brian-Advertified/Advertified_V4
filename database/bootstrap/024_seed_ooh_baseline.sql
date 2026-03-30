insert into media_outlet (
    id,
    code,
    name,
    media_type,
    coverage_type,
    catalog_health,
    operator_name,
    is_national,
    has_pricing,
    target_audience,
    data_source_enrichment
)
values (
    'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11',
    'ooh_jhb_starter',
    'JHB Starter OOH Network',
    'ooh',
    'regional',
    'strong',
    'Advertified OOH',
    false,
    true,
    'Retail shoppers and commuter audiences in Johannesburg',
    'bootstrap_ooh_seed_v1'
)
on conflict (code) do update
set
    name = excluded.name,
    media_type = excluded.media_type,
    coverage_type = excluded.coverage_type,
    catalog_health = excluded.catalog_health,
    operator_name = excluded.operator_name,
    is_national = excluded.is_national,
    has_pricing = true,
    target_audience = excluded.target_audience,
    data_source_enrichment = excluded.data_source_enrichment,
    updated_at = now();

insert into media_outlet_geography (
    id,
    media_outlet_id,
    province_code,
    city_name,
    geography_type
)
values (
    '2b3c60f2-7be7-4e24-8d88-7f3f0a5f2f22',
    'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11',
    'gauteng',
    'johannesburg',
    'city'
)
on conflict (media_outlet_id, province_code, city_name, geography_type) do update
set
    province_code = excluded.province_code,
    city_name = excluded.city_name,
    geography_type = excluded.geography_type;

insert into media_outlet_language (
    id,
    media_outlet_id,
    language_code,
    is_primary
)
values (
    '3a3bc664-8e8e-4e4e-9af8-cf9a0c7f3311',
    'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11',
    'english',
    true
)
on conflict (media_outlet_id, language_code) do update
set
    is_primary = excluded.is_primary;

insert into media_outlet_keyword (
    id,
    media_outlet_id,
    keyword
)
values (
    '6b2e0b94-fec4-4b3f-bf17-8437d8e826b9',
    'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11',
    'commuter routes'
)
on conflict (media_outlet_id, keyword) do nothing;

insert into media_outlet_pricing_package (
    id,
    media_outlet_id,
    package_name,
    package_type,
    investment_zar,
    cost_per_month_zar,
    duration_months,
    notes,
    source_name,
    is_active
)
values (
    '4d7fd79e-74db-4e07-a6d1-7b8f1d2a4422',
    'c1d47f7a-5d7d-4a1f-9a6b-7f9a5e3f4b11',
    'OOH Starter Package',
    'monthly',
    42000.00,
    42000.00,
    1,
    'Baseline OOH package seed',
    'bootstrap seed',
    true
)
on conflict (id) do update
set
    package_name = excluded.package_name,
    package_type = excluded.package_type,
    investment_zar = excluded.investment_zar,
    cost_per_month_zar = excluded.cost_per_month_zar,
    duration_months = excluded.duration_months,
    notes = excluded.notes,
    source_name = excluded.source_name,
    is_active = true,
    updated_at = now();

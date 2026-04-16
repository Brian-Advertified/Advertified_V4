create table if not exists radio_inventory_intelligence (
    id uuid primary key default gen_random_uuid(),
    media_outlet_code text not null,
    station_name text not null,
    inventory_scope varchar(30) not null default 'slot',
    source_type varchar(30) not null default 'radio_slot',
    internal_key text not null,
    slot_label text null,
    day_group text null,
    package_name text null,
    broadcast_frequency text null,
    coverage_type text null,
    province_codes_json jsonb not null default '[]'::jsonb,
    city_labels_json jsonb not null default '[]'::jsonb,
    language_codes_json jsonb not null default '[]'::jsonb,
    station_tier varchar(50) null,
    station_format varchar(80) null,
    audience_income_fit varchar(50) null,
    premium_mass_fit varchar(50) null,
    price_positioning_fit varchar(50) null,
    youth_fit varchar(20) null,
    family_fit varchar(20) null,
    professional_fit varchar(20) null,
    commuter_fit varchar(20) null,
    high_value_client_fit varchar(20) null,
    business_decision_maker_fit varchar(20) null,
    household_decision_maker_fit varchar(20) null,
    morning_drive_fit varchar(20) null,
    workday_fit varchar(20) null,
    afternoon_drive_fit varchar(20) null,
    evening_fit varchar(20) null,
    weekend_fit varchar(20) null,
    urban_rural_fit varchar(30) null,
    language_context_fit varchar(80) null,
    buying_behaviour_fit varchar(120) null,
    brand_safety_fit varchar(30) null,
    objective_fit_primary varchar(80) null,
    objective_fit_secondary varchar(80) null,
    audience_age_skew varchar(80) null,
    audience_gender_skew varchar(50) null,
    content_environment varchar(120) null,
    presenter_or_show_context text null,
    primary_audience_tags_json jsonb not null default '[]'::jsonb,
    secondary_audience_tags_json jsonb not null default '[]'::jsonb,
    recommendation_tags_json jsonb not null default '[]'::jsonb,
    intelligence_notes text null,
    source_urls_json jsonb not null default '[]'::jsonb,
    source_file text null,
    data_confidence varchar(20) null,
    updated_by text null,
    is_active boolean not null default true,
    metadata_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_radio_inventory_intelligence_internal_key
    on radio_inventory_intelligence (lower(internal_key));

create index if not exists ix_radio_inventory_intelligence_station_scope
    on radio_inventory_intelligence (media_outlet_code, inventory_scope, source_type);

alter table if exists radio_inventory_intelligence
    add column if not exists household_decision_maker_fit varchar(20) null;

alter table if exists radio_inventory_intelligence
    add column if not exists source_file text null;

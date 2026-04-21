create table if not exists ooh_inventory_intelligence (
    id uuid primary key default gen_random_uuid(),
    supplier text not null,
    site_code text null,
    site_name text not null,
    city text null,
    suburb text null,
    province text null,
    media_type text null,
    address text null,
    latitude double precision null,
    longitude double precision null,
    is_available boolean not null default true,
    discounted_rate_zar numeric(18,2) null,
    rate_card_zar numeric(18,2) null,
    monthly_rate_zar numeric(18,2) null,
    traffic_count bigint null,
    scope_level varchar(30) not null default 'site',
    venue_type varchar(80) null,
    premium_mass_fit varchar(50) null,
    price_positioning_fit varchar(50) null,
    audience_income_fit varchar(50) null,
    youth_fit varchar(20) null,
    family_fit varchar(20) null,
    professional_fit varchar(20) null,
    commuter_fit varchar(20) null,
    tourist_fit varchar(20) null,
    high_value_shopper_fit varchar(20) null,
    audience_age_skew varchar(80) null,
    audience_gender_skew varchar(50) null,
    dwell_time_score varchar(20) null,
    environment_type varchar(80) null,
    buying_behaviour_fit varchar(120) null,
    primary_audience_tags_json jsonb not null default '[]'::jsonb,
    secondary_audience_tags_json jsonb not null default '[]'::jsonb,
    recommendation_tags_json jsonb not null default '[]'::jsonb,
    intelligence_notes text null,
    data_confidence varchar(20) null,
    updated_by text null,
    is_active boolean not null default true,
    metadata_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_ooh_inventory_intelligence_site_lookup
    on ooh_inventory_intelligence (
        lower(coalesce(supplier, '')),
        lower(coalesce(site_code, '')),
        lower(coalesce(site_name, '')),
        lower(coalesce(city, '')),
        lower(coalesce(suburb, '')),
        lower(coalesce(province, ''))
    );

create index if not exists ix_ooh_inventory_intelligence_supplier_site
    on ooh_inventory_intelligence (supplier, site_name);

create index if not exists ix_ooh_inventory_intelligence_active_geography
    on ooh_inventory_intelligence (is_active, province, city, suburb);

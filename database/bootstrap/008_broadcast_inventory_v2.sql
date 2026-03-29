create table if not exists media_outlet (
    id uuid primary key,
    code text not null unique,
    name text not null,
    media_type text not null,
    coverage_type text not null,
    catalog_health text not null,
    operator_name text null,
    is_national boolean not null default false,
    has_pricing boolean not null default false,
    language_notes text null,
    audience_age_skew text null,
    audience_gender_skew text null,
    audience_lsm_range text null,
    audience_racial_skew text null,
    audience_urban_rural text null,
    broadcast_frequency text null,
    listenership_daily bigint null,
    listenership_weekly bigint null,
    listenership_period text null,
    target_audience text null,
    data_source_enrichment text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists media_outlet_keyword (
    id uuid primary key,
    media_outlet_id uuid not null references media_outlet(id) on delete cascade,
    keyword text not null,
    unique (media_outlet_id, keyword)
);

create table if not exists media_outlet_language (
    id uuid primary key,
    media_outlet_id uuid not null references media_outlet(id) on delete cascade,
    language_code text not null,
    is_primary boolean not null default true,
    unique (media_outlet_id, language_code)
);

create table if not exists media_outlet_geography (
    id uuid primary key,
    media_outlet_id uuid not null references media_outlet(id) on delete cascade,
    province_code text null,
    city_name text null,
    geography_type text not null,
    unique nulls not distinct (media_outlet_id, province_code, city_name, geography_type)
);

create table if not exists media_outlet_pricing_package (
    id uuid primary key,
    media_outlet_id uuid not null references media_outlet(id) on delete cascade,
    package_name text not null,
    package_type text null,
    exposure_count integer null,
    monthly_exposure_count integer null,
    value_zar numeric(18,2) null,
    discount_zar numeric(18,2) null,
    saving_zar numeric(18,2) null,
    investment_zar numeric(18,2) null,
    cost_per_month_zar numeric(18,2) null,
    duration_months integer null,
    duration_weeks integer null,
    notes text null,
    source_name text null,
    source_date date null,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists media_outlet_slot_rate (
    id uuid primary key,
    media_outlet_id uuid not null references media_outlet(id) on delete cascade,
    day_group text not null,
    start_time time not null,
    end_time time not null,
    ad_duration_seconds integer not null default 30,
    rate_zar numeric(18,2) not null,
    rate_type text not null default 'spot',
    source_name text null,
    source_date date null,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

drop table if exists matcher_run_result;
drop table if exists matcher_run;
drop table if exists media_outlet_data_quality_flag;
drop table if exists media_outlet_metric;

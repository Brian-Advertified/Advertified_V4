alter table if exists radio_inventory_intelligence
    add column if not exists household_decision_maker_fit varchar(20) null;

alter table if exists radio_inventory_intelligence
    add column if not exists genre_fit varchar(80) null;

alter table if exists tv_inventory_intelligence
    add column if not exists household_decision_maker_fit varchar(20) null;

alter table if exists ooh_inventory_intelligence
    add column if not exists media_type text null,
    add column if not exists address text null,
    add column if not exists latitude double precision null,
    add column if not exists longitude double precision null,
    add column if not exists is_available boolean not null default true,
    add column if not exists discounted_rate_zar numeric(18,2) null,
    add column if not exists rate_card_zar numeric(18,2) null,
    add column if not exists monthly_rate_zar numeric(18,2) null,
    add column if not exists traffic_count bigint null,
    add column if not exists scope_level varchar(30) not null default 'site',
    add column if not exists venue_type varchar(80) null,
    add column if not exists premium_mass_fit varchar(50) null,
    add column if not exists price_positioning_fit varchar(50) null,
    add column if not exists audience_income_fit varchar(50) null,
    add column if not exists youth_fit varchar(20) null,
    add column if not exists family_fit varchar(20) null,
    add column if not exists professional_fit varchar(20) null,
    add column if not exists commuter_fit varchar(20) null,
    add column if not exists tourist_fit varchar(20) null,
    add column if not exists high_value_shopper_fit varchar(20) null,
    add column if not exists audience_age_skew varchar(80) null,
    add column if not exists audience_gender_skew varchar(50) null,
    add column if not exists dwell_time_score varchar(20) null,
    add column if not exists environment_type varchar(80) null,
    add column if not exists buying_behaviour_fit varchar(120) null,
    add column if not exists primary_audience_tags_json jsonb not null default '[]'::jsonb,
    add column if not exists secondary_audience_tags_json jsonb not null default '[]'::jsonb,
    add column if not exists recommendation_tags_json jsonb not null default '[]'::jsonb,
    add column if not exists intelligence_notes text null,
    add column if not exists data_confidence varchar(20) null,
    add column if not exists updated_by text null,
    add column if not exists is_active boolean not null default true,
    add column if not exists metadata_json jsonb not null default '{}'::jsonb,
    add column if not exists created_at timestamptz not null default now(),
    add column if not exists updated_at timestamptz not null default now();

create table if not exists ai_ad_variants (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    campaign_creative_id uuid null references campaign_creatives(id) on delete set null,
    platform varchar(40) not null,
    channel varchar(40) not null default 'Digital',
    language varchar(40) not null default 'English',
    template_id integer null,
    voice_pack_id uuid null references ai_voice_packs(id) on delete set null,
    voice_pack_name varchar(150) null,
    script text not null,
    audio_asset_url text null,
    platform_ad_id varchar(160) null,
    status varchar(30) not null default 'draft',
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    published_at timestamp without time zone null
);

create index if not exists ix_ai_ad_variants_campaign_id on ai_ad_variants(campaign_id);
create index if not exists ix_ai_ad_variants_platform on ai_ad_variants(platform);
create index if not exists ix_ai_ad_variants_status on ai_ad_variants(status);
create index if not exists ix_ai_ad_variants_published_at on ai_ad_variants(published_at desc);

create table if not exists ai_ad_metrics (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    ad_variant_id uuid not null references ai_ad_variants(id) on delete cascade,
    platform varchar(40) not null,
    source varchar(30) not null default 'sync',
    impressions integer not null default 0,
    clicks integer not null default 0,
    conversions integer not null default 0,
    cost_zar numeric(12,2) not null default 0,
    ctr numeric(8,4) not null default 0,
    conversion_rate numeric(8,4) not null default 0,
    recorded_at timestamp without time zone not null default now(),
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_ai_ad_metrics_campaign_id on ai_ad_metrics(campaign_id);
create index if not exists ix_ai_ad_metrics_ad_variant_id on ai_ad_metrics(ad_variant_id);
create index if not exists ix_ai_ad_metrics_platform on ai_ad_metrics(platform);
create index if not exists ix_ai_ad_metrics_recorded_at on ai_ad_metrics(recorded_at desc);

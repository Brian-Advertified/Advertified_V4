create table if not exists package_band_ai_entitlements (
    package_band_id uuid primary key references package_bands(id) on delete cascade,
    max_ad_variants integer not null default 1,
    allowed_ad_platforms_json jsonb not null default '["Meta"]'::jsonb,
    allow_ad_metrics_sync boolean not null default true,
    allow_ad_auto_optimize boolean not null default false,
    allowed_voice_pack_tiers_json jsonb not null default '["standard"]'::jsonb,
    max_ad_regenerations integer not null default 1,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now()
);

insert into package_band_ai_entitlements (
    package_band_id,
    max_ad_variants,
    allowed_ad_platforms_json,
    allow_ad_metrics_sync,
    allow_ad_auto_optimize,
    allowed_voice_pack_tiers_json,
    max_ad_regenerations
)
select
    b.id,
    case lower(b.code)
        when 'launch' then 1
        when 'scale' then 3
        when 'dominate' then 6
        when 'dominance' then 6
        else 1
    end as max_ad_variants,
    case lower(b.code)
        when 'dominance' then '["Meta","GoogleAds"]'::jsonb
        when 'dominate' then '["Meta","GoogleAds"]'::jsonb
        else '["Meta"]'::jsonb
    end as allowed_ad_platforms_json,
    true as allow_ad_metrics_sync,
    case lower(b.code)
        when 'launch' then false
        else true
    end as allow_ad_auto_optimize,
    case lower(b.code)
        when 'launch' then '["standard"]'::jsonb
        when 'scale' then '["standard","premium"]'::jsonb
        else '["standard","premium","exclusive"]'::jsonb
    end as allowed_voice_pack_tiers_json,
    case lower(b.code)
        when 'launch' then 1
        when 'scale' then 3
        else 5
    end as max_ad_regenerations
from package_bands b
where not exists (
    select 1
    from package_band_ai_entitlements e
    where e.package_band_id = b.id
);

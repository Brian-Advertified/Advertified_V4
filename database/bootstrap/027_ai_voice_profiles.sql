create table if not exists ai_voice_profiles (
    id uuid primary key default gen_random_uuid(),
    provider varchar(40) not null,
    label varchar(120) not null,
    voice_id varchar(120) not null,
    language varchar(40) null,
    is_active boolean not null default true,
    sort_order integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists uq_ai_voice_profiles_provider_label
    on ai_voice_profiles(provider, label);

create index if not exists ix_ai_voice_profiles_provider_active_sort
    on ai_voice_profiles(provider, is_active, sort_order);


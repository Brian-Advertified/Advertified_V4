create table if not exists campaign_creatives (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    source_creative_system_id uuid references campaign_creative_systems(id) on delete set null,
    channel varchar(40) not null,
    language varchar(40) not null default 'English',
    creative_type varchar(80) not null,
    json_payload jsonb not null,
    score numeric(5,2),
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now()
);

create index if not exists ix_campaign_creatives_campaign_id on campaign_creatives(campaign_id);
create index if not exists ix_campaign_creatives_channel on campaign_creatives(channel);
create index if not exists ix_campaign_creatives_created_at on campaign_creatives(created_at desc);

create table if not exists creative_scores (
    id uuid primary key default gen_random_uuid(),
    campaign_creative_id uuid not null references campaign_creatives(id) on delete cascade,
    metric_name varchar(80) not null,
    metric_value numeric(5,2) not null,
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_creative_scores_campaign_creative_id on creative_scores(campaign_creative_id);
create unique index if not exists uq_creative_scores_creative_metric on creative_scores(campaign_creative_id, metric_name);

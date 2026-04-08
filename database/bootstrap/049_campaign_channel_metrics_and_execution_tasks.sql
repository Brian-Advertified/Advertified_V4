create table if not exists campaign_channel_metrics (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    channel text not null,
    provider text not null,
    metric_date date not null,
    spend_zar numeric(12,2) not null default 0,
    impressions bigint not null default 0,
    clicks integer not null default 0,
    leads integer not null default 0,
    attributed_revenue_zar numeric(12,2) not null default 0,
    cpl_zar numeric(12,2),
    roas numeric(12,4),
    source_type text not null default 'ad_platform_sync',
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now()
);

create unique index if not exists ux_campaign_channel_metrics_campaign_channel_provider_date
    on campaign_channel_metrics (campaign_id, channel, provider, metric_date);

create index if not exists ix_campaign_channel_metrics_campaign_id
    on campaign_channel_metrics (campaign_id);

create index if not exists ix_campaign_channel_metrics_metric_date
    on campaign_channel_metrics (metric_date desc);

create table if not exists campaign_execution_tasks (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    task_key text not null,
    title text not null,
    details text,
    status text not null default 'open',
    sort_order integer not null default 100,
    due_at timestamp without time zone,
    completed_at timestamp without time zone,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now()
);

create unique index if not exists ux_campaign_execution_tasks_campaign_task_key
    on campaign_execution_tasks (campaign_id, task_key);

create index if not exists ix_campaign_execution_tasks_campaign_status
    on campaign_execution_tasks (campaign_id, status);

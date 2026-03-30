create table if not exists campaign_creative_systems (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    created_by_user_id uuid references user_accounts(id) on delete set null,
    prompt text not null,
    iteration_label varchar(100),
    input_json jsonb not null,
    output_json jsonb not null,
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_campaign_creative_systems_campaign_id on campaign_creative_systems(campaign_id);
create index if not exists ix_campaign_creative_systems_created_at on campaign_creative_systems(created_at desc);

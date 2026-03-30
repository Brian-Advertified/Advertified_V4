create table if not exists agent_area_assignments (
    id uuid primary key default gen_random_uuid(),
    agent_user_id uuid not null references user_accounts(id) on delete cascade,
    area_code varchar(50) not null references package_area_profiles(cluster_code) on delete cascade,
    created_at timestamptz not null default now()
);

create unique index if not exists uq_agent_area_assignments_area_code
    on agent_area_assignments (area_code);

create unique index if not exists uq_agent_area_assignments_agent_user_id_area_code
    on agent_area_assignments (agent_user_id, area_code);

create index if not exists ix_agent_area_assignments_agent_user_id
    on agent_area_assignments (agent_user_id);

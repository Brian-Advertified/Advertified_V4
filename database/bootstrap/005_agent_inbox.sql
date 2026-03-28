alter table campaigns
add column if not exists assigned_agent_user_id uuid references user_accounts(id) on delete set null;

alter table campaigns
add column if not exists assigned_at timestamp without time zone null;

create index if not exists ix_campaigns_assigned_agent_user_id
    on campaigns (assigned_agent_user_id);

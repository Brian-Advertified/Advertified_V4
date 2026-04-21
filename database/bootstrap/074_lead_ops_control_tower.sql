alter table if exists leads
    add column if not exists owner_agent_user_id uuid null references user_accounts(id) on delete set null,
    add column if not exists first_contacted_at timestamp null,
    add column if not exists last_contacted_at timestamp null,
    add column if not exists next_follow_up_at timestamp null,
    add column if not exists sla_due_at timestamp null,
    add column if not exists last_outcome text null;

create index if not exists ix_leads_owner_agent_user_id on leads(owner_agent_user_id);

with latest_campaign_owner as (
    select
        pl.source_lead_id as lead_id,
        c.assigned_agent_user_id,
        row_number() over (
            partition by pl.source_lead_id
            order by
                case when c.assigned_agent_user_id is not null then 0 else 1 end,
                c.updated_at desc,
                c.created_at desc
        ) as row_num
    from prospect_leads pl
    join campaigns c on c.prospect_lead_id = pl.id
    where pl.source_lead_id is not null
),
latest_prospect_owner as (
    select
        pl.source_lead_id as lead_id,
        pl.owner_agent_user_id,
        row_number() over (
            partition by pl.source_lead_id
            order by
                case when pl.owner_agent_user_id is not null then 0 else 1 end,
                pl.updated_at desc,
                pl.created_at desc
        ) as row_num
    from prospect_leads pl
    where pl.source_lead_id is not null
),
single_action_owner as (
    select
        la.lead_id,
        case
            when count(distinct la.assigned_agent_user_id) = 1
                then min(la.assigned_agent_user_id::text)::uuid
            else null
        end as assigned_agent_user_id
    from lead_actions la
    where la.status = 'open'
      and la.assigned_agent_user_id is not null
    group by la.lead_id
),
first_interaction as (
    select lead_id, min(created_at) as first_contacted_at
    from lead_interactions
    group by lead_id
),
last_interaction as (
    select lead_id, max(created_at) as last_contacted_at
    from lead_interactions
    group by lead_id
),
prospect_rollup as (
    select
        pl.source_lead_id as lead_id,
        max(pl.last_contacted_at) as prospect_last_contacted_at,
        min(pl.next_follow_up_at) as next_follow_up_at,
        min(pl.sla_due_at) as sla_due_at,
        max(nullif(pl.last_outcome, '')) as last_outcome
    from prospect_leads pl
    where pl.source_lead_id is not null
    group by pl.source_lead_id
)
update leads l
set
    owner_agent_user_id = coalesce(campaign_owner.assigned_agent_user_id, prospect_owner.owner_agent_user_id, action_owner.assigned_agent_user_id, l.owner_agent_user_id),
    first_contacted_at = coalesce(l.first_contacted_at, first_interaction.first_contacted_at, prospect_rollup.prospect_last_contacted_at),
    last_contacted_at = coalesce(
        greatest(
            coalesce(l.last_contacted_at, timestamp 'epoch'),
            coalesce(last_interaction.last_contacted_at, timestamp 'epoch'),
            coalesce(prospect_rollup.prospect_last_contacted_at, timestamp 'epoch')
        ),
        l.last_contacted_at
    ),
    next_follow_up_at = coalesce(prospect_rollup.next_follow_up_at, l.next_follow_up_at),
    sla_due_at = coalesce(prospect_rollup.sla_due_at, l.sla_due_at),
    last_outcome = coalesce(prospect_rollup.last_outcome, l.last_outcome)
from latest_campaign_owner campaign_owner
full outer join latest_prospect_owner prospect_owner
    on prospect_owner.lead_id = campaign_owner.lead_id
    and prospect_owner.row_num = 1
full outer join single_action_owner action_owner
    on action_owner.lead_id = coalesce(campaign_owner.lead_id, prospect_owner.lead_id)
left join first_interaction
    on first_interaction.lead_id = coalesce(campaign_owner.lead_id, prospect_owner.lead_id, action_owner.lead_id)
left join last_interaction
    on last_interaction.lead_id = coalesce(campaign_owner.lead_id, prospect_owner.lead_id, action_owner.lead_id)
left join prospect_rollup
    on prospect_rollup.lead_id = coalesce(campaign_owner.lead_id, prospect_owner.lead_id, action_owner.lead_id)
where l.id = coalesce(campaign_owner.lead_id, prospect_owner.lead_id, action_owner.lead_id)
  and (campaign_owner.row_num = 1 or campaign_owner.row_num is null);

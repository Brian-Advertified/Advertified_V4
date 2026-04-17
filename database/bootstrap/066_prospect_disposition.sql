alter table campaigns
    add column if not exists prospect_disposition_status varchar(20) not null default 'open',
    add column if not exists prospect_disposition_reason varchar(100),
    add column if not exists prospect_disposition_notes text,
    add column if not exists prospect_disposition_closed_at timestamptz,
    add column if not exists prospect_disposition_closed_by_user_id uuid;

update campaigns
set prospect_disposition_status = 'open'
where prospect_disposition_status is null or btrim(prospect_disposition_status) = '';

create index if not exists ix_campaigns_prospect_disposition_status
    on campaigns (prospect_disposition_status);

create index if not exists ix_campaigns_prospect_disposition_closed_by_user_id
    on campaigns (prospect_disposition_closed_by_user_id);

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'campaigns_prospect_disposition_closed_by_user_id_fkey'
    ) then
        alter table campaigns
            add constraint campaigns_prospect_disposition_closed_by_user_id_fkey
            foreign key (prospect_disposition_closed_by_user_id)
            references user_accounts (id)
            on delete set null;
    end if;
end $$;

insert into form_option_items (option_set_key, value, label, sort_order)
values
    ('prospect_disposition_reasons', 'no_response', 'No response after follow-up', 10),
    ('prospect_disposition_reasons', 'budget_not_approved', 'Budget not approved', 20),
    ('prospect_disposition_reasons', 'timing_not_right', 'Timing not right', 30),
    ('prospect_disposition_reasons', 'lost_to_competitor', 'Lost to competitor', 40),
    ('prospect_disposition_reasons', 'not_a_fit', 'Not a fit', 50),
    ('prospect_disposition_reasons', 'client_declined', 'Client declined', 60)
on conflict (option_set_key, value) do update
set label = excluded.label,
    sort_order = excluded.sort_order,
    is_active = true,
    updated_at = now();

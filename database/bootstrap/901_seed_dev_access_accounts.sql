begin;

insert into user_accounts
(
    id,
    full_name,
    email,
    phone,
    password_hash,
    role,
    account_status,
    is_sa_citizen,
    email_verified,
    phone_verified,
    created_at,
    updated_at
)
values
(
    gen_random_uuid(),
    'Dev Admin',
    'admin@advertified.com',
    '+27110000004',
    'AQAAAAIAAYagAAAAEOuFgypemLsIZ6+s5WI1nOwDIU6hMZwVKuj3e6clW7Njj8DL3+8kPnnlXSITfW7uiQ==',
    'admin',
    'active',
    true,
    true,
    true,
    now(),
    now()
),
(
    gen_random_uuid(),
    'Dev Agent',
    'agent@advertified.com',
    '+27110000002',
    'AQAAAAIAAYagAAAAENZxVplyrLzDyidfsMlmf7BJoqcPS4AgxleffcrsPmT18AWocq/nQLGzFhUfch4WOQ==',
    'agent',
    'active',
    true,
    true,
    true,
    now(),
    now()
)
on conflict (email)
do update set
    full_name = excluded.full_name,
    phone = excluded.phone,
    password_hash = excluded.password_hash,
    role = excluded.role,
    account_status = excluded.account_status,
    is_sa_citizen = excluded.is_sa_citizen,
    email_verified = excluded.email_verified,
    phone_verified = excluded.phone_verified,
    updated_at = now();

-- DEV reset: keep only operations users (admin + agent) and remove all other user accounts
-- with their campaign/order data so QA can test clean client onboarding repeatedly.
with target_users as (
    select id
    from user_accounts
    where role not in ('admin', 'agent')
),
target_campaigns as (
    select id
    from campaigns
    where user_id in (select id from target_users)
),
target_orders as (
    select id
    from package_orders
    where user_id in (select id from target_users)
)
delete from campaign_messages
where sender_user_id in (select id from target_users);

with target_users as (
    select id
    from user_accounts
    where role not in ('admin', 'agent')
)
delete from campaign_conversations
where client_user_id in (select id from target_users);

with target_users as (
    select id
    from user_accounts
    where role not in ('admin', 'agent')
)
delete from campaigns
where user_id in (select id from target_users);

with target_users as (
    select id
    from user_accounts
    where role not in ('admin', 'agent')
)
delete from package_orders
where user_id in (select id from target_users);

with target_users as (
    select id
    from user_accounts
    where role not in ('admin', 'agent')
)
delete from email_verification_tokens
where user_id in (select id from target_users);

with target_users as (
    select id
    from user_accounts
    where role not in ('admin', 'agent')
)
do $$
begin
    if exists (
        select 1
        from information_schema.tables
        where table_schema = 'public'
          and table_name = 'session_tokens'
    ) then
        execute '
            with target_users as (
                select id
                from user_accounts
                where role not in (''admin'', ''agent'')
            )
            delete from session_tokens
            where user_id in (select id from target_users)
        ';
    end if;
end $$;

do $$
begin
    if exists (
        select 1
        from information_schema.tables
        where table_schema = 'public'
          and table_name = 'password_reset_tokens'
    ) then
        execute '
            with target_users as (
                select id
                from user_accounts
                where role not in (''admin'', ''agent'')
            )
            delete from password_reset_tokens
            where user_id in (select id from target_users)
        ';
    end if;
end $$;

delete from user_accounts
where role not in ('admin', 'agent');

commit;

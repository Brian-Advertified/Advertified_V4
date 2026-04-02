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
    'admin@advertfified.com',
    '+27110000001',
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
),
(
    gen_random_uuid(),
    'Dev Creative Director',
    'crative@advertified.com',
    '+27110000003',
    'AQAAAAIAAYagAAAAEPLQiVI+nt1mhC64ZNkKBR4lH6Rm7LIMOGPY3a1ZsiQvhujpSWGDSD8/WXWxrNLePQ==',
    'creative_director',
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

commit;

create table if not exists consent_preferences (
    id uuid primary key default gen_random_uuid(),
    user_id uuid null references user_accounts(id) on delete set null,
    browser_id varchar(200) not null,
    necessary_cookies boolean not null default true,
    analytics_cookies boolean not null default false,
    marketing_cookies boolean not null default false,
    privacy_accepted boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists uq_consent_preferences_browser_id
    on consent_preferences(browser_id);

create index if not exists ix_consent_preferences_browser_id
    on consent_preferences(browser_id);

create index if not exists ix_consent_preferences_user_id
    on consent_preferences(user_id);

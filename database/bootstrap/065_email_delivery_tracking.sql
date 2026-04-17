create table if not exists email_delivery_provider_settings
(
    provider_key varchar(50) primary key,
    display_name varchar(120) not null,
    webhook_enabled boolean not null default false,
    webhook_signing_secret text null,
    webhook_endpoint_path varchar(200) null,
    allowed_event_types_json jsonb not null default '[]'::jsonb,
    max_signature_age_seconds integer not null default 300,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

insert into email_delivery_provider_settings
(
    provider_key,
    display_name,
    webhook_enabled,
    webhook_signing_secret,
    webhook_endpoint_path,
    allowed_event_types_json,
    max_signature_age_seconds
)
values
(
    'resend',
    'Resend',
    false,
    null,
    '/webhooks/email-delivery/resend',
    '["email.sent","email.delivered","email.delivery_delayed","email.bounced","email.failed","email.complained","email.opened","email.clicked"]'::jsonb,
    300
)
on conflict (provider_key) do update
set
    display_name = excluded.display_name,
    webhook_endpoint_path = excluded.webhook_endpoint_path,
    allowed_event_types_json = excluded.allowed_event_types_json,
    max_signature_age_seconds = excluded.max_signature_age_seconds,
    updated_at = now();

create table if not exists email_delivery_messages
(
    id uuid primary key default gen_random_uuid(),
    provider_key varchar(50) not null,
    template_name varchar(120) not null,
    sender_key varchar(50) not null,
    delivery_purpose varchar(80) not null,
    status varchar(40) not null default 'pending',
    from_address varchar(255) not null,
    recipient_email varchar(255) not null,
    subject varchar(500) not null,
    campaign_id uuid null references campaigns (id) on delete cascade,
    recommendation_id uuid null references campaign_recommendations (id) on delete set null,
    recommendation_revision_number integer null,
    recipient_user_id uuid null references user_accounts (id) on delete set null,
    prospect_lead_id uuid null references prospect_leads (id) on delete set null,
    provider_message_id varchar(120) null,
    provider_broadcast_id varchar(120) null,
    latest_event_type varchar(80) null,
    latest_event_at timestamptz null,
    accepted_at timestamptz null,
    delivered_at timestamptz null,
    opened_at timestamptz null,
    clicked_at timestamptz null,
    complained_at timestamptz null,
    bounced_at timestamptz null,
    failed_at timestamptz null,
    archived_at timestamptz null,
    archived_path text null,
    last_error text null,
    metadata_json jsonb null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists uq_email_delivery_messages_provider_message_id
    on email_delivery_messages (provider_key, provider_message_id)
    where provider_message_id is not null;

create index if not exists ix_email_delivery_messages_campaign_id
    on email_delivery_messages (campaign_id, created_at desc);

create index if not exists ix_email_delivery_messages_recommendation_revision
    on email_delivery_messages (campaign_id, recommendation_revision_number, created_at desc);

create index if not exists ix_email_delivery_messages_recipient_email
    on email_delivery_messages (recipient_email);

create table if not exists email_delivery_events
(
    id uuid primary key default gen_random_uuid(),
    provider_key varchar(50) not null,
    email_delivery_message_id uuid null references email_delivery_messages (id) on delete cascade,
    provider_webhook_message_id varchar(120) null,
    provider_message_id varchar(120) null,
    provider_event_type varchar(80) not null,
    recipient_email varchar(255) null,
    event_created_at timestamptz not null,
    received_at timestamptz not null default now(),
    processing_status varchar(40) not null default 'received',
    processing_notes text null,
    payload_json jsonb not null
);

create unique index if not exists uq_email_delivery_events_provider_webhook_message_id
    on email_delivery_events (provider_key, provider_webhook_message_id)
    where provider_webhook_message_id is not null;

create index if not exists ix_email_delivery_events_message_id
    on email_delivery_events (email_delivery_message_id, event_created_at desc);

create table if not exists email_delivery_webhook_audits
(
    id uuid primary key default gen_random_uuid(),
    provider_key varchar(50) not null,
    request_path varchar(200) not null,
    webhook_message_id varchar(120) null,
    event_type varchar(80) null,
    signature_valid boolean not null default false,
    processing_status varchar(40) not null default 'received',
    processing_notes text null,
    headers_json jsonb null,
    payload_json jsonb null,
    created_at timestamptz not null default now(),
    processed_at timestamptz null
);

create index if not exists ix_email_delivery_webhook_audits_provider_created
    on email_delivery_webhook_audits (provider_key, created_at desc);

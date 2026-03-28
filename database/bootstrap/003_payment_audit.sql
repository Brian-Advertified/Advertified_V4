create table if not exists payment_provider_requests (
    id uuid primary key default gen_random_uuid(),
    package_order_id uuid null references package_orders(id) on delete set null,
    provider varchar(50) not null,
    event_type varchar(100) not null,
    external_reference varchar(200) null,
    request_url text not null,
    request_headers_json jsonb not null default '{}'::jsonb,
    request_body_json text not null,
    response_status_code integer null,
    response_headers_json jsonb null,
    response_body_text text null,
    created_at timestamptz not null default now(),
    completed_at timestamptz null
);

create index if not exists ix_payment_provider_requests_package_order_id
    on payment_provider_requests(package_order_id);

create index if not exists ix_payment_provider_requests_provider_created_at
    on payment_provider_requests(provider, created_at desc);

create table if not exists payment_provider_webhooks (
    id uuid primary key default gen_random_uuid(),
    package_order_id uuid null references package_orders(id) on delete set null,
    provider varchar(50) not null,
    webhook_path varchar(200) not null,
    headers_json jsonb not null default '{}'::jsonb,
    body_json text not null,
    processed_status varchar(50) not null,
    processed_message text null,
    created_at timestamptz not null default now(),
    processed_at timestamptz null
);

create index if not exists ix_payment_provider_webhooks_package_order_id
    on payment_provider_webhooks(package_order_id);

create index if not exists ix_payment_provider_webhooks_provider_created_at
    on payment_provider_webhooks(provider, created_at desc);

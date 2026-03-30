alter table campaigns
    add column if not exists paused_at timestamp without time zone,
    add column if not exists total_paused_days integer not null default 0,
    add column if not exists pause_reason text;

create table if not exists campaign_assets (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    uploaded_by_user_id uuid references user_accounts(id) on delete set null,
    asset_type varchar(50) not null,
    display_name varchar(255) not null,
    storage_object_key text not null,
    public_url text,
    content_type varchar(255),
    size_bytes bigint not null default 0,
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_campaign_assets_campaign_id on campaign_assets(campaign_id);
create index if not exists ix_campaign_assets_asset_type on campaign_assets(asset_type);

create table if not exists campaign_supplier_bookings (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    created_by_user_id uuid references user_accounts(id) on delete set null,
    proof_asset_id uuid references campaign_assets(id) on delete set null,
    supplier_or_station varchar(255) not null,
    channel varchar(50) not null,
    booking_status varchar(50) not null default 'planned',
    committed_amount numeric(12,2) not null default 0,
    booked_at timestamp without time zone,
    live_from date,
    live_to date,
    notes text,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now()
);

create index if not exists ix_campaign_supplier_bookings_campaign_id on campaign_supplier_bookings(campaign_id);
create index if not exists ix_campaign_supplier_bookings_live_window on campaign_supplier_bookings(live_from, live_to);

create table if not exists campaign_delivery_reports (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    supplier_booking_id uuid references campaign_supplier_bookings(id) on delete set null,
    evidence_asset_id uuid references campaign_assets(id) on delete set null,
    created_by_user_id uuid references user_accounts(id) on delete set null,
    report_type varchar(50) not null,
    headline varchar(200) not null,
    summary text,
    reported_at timestamp without time zone,
    impressions bigint,
    plays_or_spots integer,
    spend_delivered numeric(12,2),
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_campaign_delivery_reports_campaign_id on campaign_delivery_reports(campaign_id);
create index if not exists ix_campaign_delivery_reports_reported_at on campaign_delivery_reports(reported_at);

create table if not exists campaign_pause_windows (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    created_by_user_id uuid references user_accounts(id) on delete set null,
    resumed_by_user_id uuid references user_accounts(id) on delete set null,
    started_at timestamp without time zone not null,
    ended_at timestamp without time zone,
    pause_reason text,
    resume_reason text,
    paused_day_count integer not null default 0,
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_campaign_pause_windows_campaign_id on campaign_pause_windows(campaign_id);

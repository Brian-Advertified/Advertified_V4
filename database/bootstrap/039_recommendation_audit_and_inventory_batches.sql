create table if not exists inventory_import_batches (
    id uuid primary key default gen_random_uuid(),
    channel_family varchar(50) not null,
    source_type varchar(50) not null,
    source_identifier varchar(500) not null,
    source_checksum varchar(128) null,
    record_count integer not null default 0,
    status varchar(30) not null,
    is_active boolean not null default false,
    metadata_json jsonb null,
    created_at timestamptz not null default now(),
    activated_at timestamptz null
);

create index if not exists ix_inventory_import_batches_channel_created
    on inventory_import_batches (channel_family, created_at desc);

create index if not exists ix_inventory_import_batches_channel_active
    on inventory_import_batches (channel_family, is_active);

alter table if exists media_outlet
    add column if not exists import_batch_id uuid null references inventory_import_batches(id) on delete set null;

create index if not exists ix_media_outlet_import_batch_id
    on media_outlet (import_batch_id);

alter table if exists campaign_recommendations
    add column if not exists request_snapshot_json jsonb null;

alter table if exists campaign_recommendations
    add column if not exists policy_snapshot_json jsonb null;

alter table if exists campaign_recommendations
    add column if not exists inventory_snapshot_json jsonb null;

create table if not exists recommendation_run_audits (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    recommendation_id uuid not null references campaign_recommendations(id) on delete cascade,
    recommendation_type varchar(100) not null,
    revision_number integer not null,
    request_snapshot_json jsonb null,
    policy_snapshot_json jsonb null,
    inventory_snapshot_json jsonb null,
    candidate_counts_json jsonb null,
    rejected_candidates_json jsonb null,
    selected_items_json jsonb null,
    fallback_flags_json jsonb null,
    budget_utilization_ratio numeric(8,4) not null default 0,
    manual_review_required boolean not null default false,
    final_rationale text null,
    created_at timestamptz not null default now()
);

create index if not exists ix_recommendation_run_audits_campaign_id
    on recommendation_run_audits (campaign_id);

create index if not exists ix_recommendation_run_audits_recommendation_id
    on recommendation_run_audits (recommendation_id);

create index if not exists ix_recommendation_run_audits_created_at
    on recommendation_run_audits (created_at desc);

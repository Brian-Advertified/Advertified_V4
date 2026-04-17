alter table if exists master_locations
    add column if not exists parent_city varchar(160),
    add column if not exists source_system varchar(80) not null default 'seed',
    add column if not exists is_verified boolean not null default true,
    add column if not exists last_seen_at timestamptz not null default now();

create index if not exists ix_master_locations_location_type on master_locations(location_type);
create index if not exists ix_master_locations_parent_city on master_locations(parent_city);
create index if not exists ix_master_locations_last_seen_at on master_locations(last_seen_at desc);

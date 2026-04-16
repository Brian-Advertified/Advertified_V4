alter table if exists campaign_briefs
    add column if not exists target_location_label varchar(240),
    add column if not exists target_location_city varchar(160),
    add column if not exists target_location_province varchar(160),
    add column if not exists target_latitude double precision,
    add column if not exists target_longitude double precision;

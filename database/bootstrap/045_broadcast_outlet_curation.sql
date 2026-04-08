alter table if exists media_outlet
    add column if not exists preserve_imported_core_metadata boolean not null default false;

alter table if exists media_outlet
    add column if not exists preserve_imported_languages boolean not null default false;

alter table if exists media_outlet
    add column if not exists preserve_imported_geography boolean not null default false;

alter table if exists media_outlet
    add column if not exists preserve_imported_keywords boolean not null default false;

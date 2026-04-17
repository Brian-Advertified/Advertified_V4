create table if not exists master_locations
(
    id uuid primary key default gen_random_uuid(),
    canonical_name text not null unique,
    location_type varchar(40) not null default 'city',
    parent_city varchar(160),
    province varchar(120),
    country varchar(120) not null default 'South Africa',
    latitude double precision,
    longitude double precision,
    source_system varchar(80) not null default 'seed',
    is_verified boolean not null default true,
    last_seen_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists master_location_aliases
(
    id uuid primary key default gen_random_uuid(),
    master_location_id uuid not null references master_locations(id) on delete cascade,
    alias text not null unique,
    created_at timestamptz not null default now()
);

create index if not exists ix_master_location_aliases_location_id on master_location_aliases(master_location_id);

create table if not exists master_industries
(
    id uuid primary key default gen_random_uuid(),
    code varchar(80) not null unique,
    label varchar(160) not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists master_industry_aliases
(
    id uuid primary key default gen_random_uuid(),
    master_industry_id uuid not null references master_industries(id) on delete cascade,
    alias text not null unique,
    created_at timestamptz not null default now()
);

create index if not exists ix_master_industry_aliases_industry_id on master_industry_aliases(master_industry_id);

create table if not exists master_languages
(
    id uuid primary key default gen_random_uuid(),
    code varchar(80) not null unique,
    label varchar(160) not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists master_language_aliases
(
    id uuid primary key default gen_random_uuid(),
    master_language_id uuid not null references master_languages(id) on delete cascade,
    alias text not null unique,
    created_at timestamptz not null default now()
);

create index if not exists ix_master_language_aliases_language_id on master_language_aliases(master_language_id);

insert into master_locations (canonical_name, location_type, province, latitude, longitude)
values
    ('Johannesburg', 'city', 'Gauteng', -26.2041, 28.0473),
    ('Pretoria', 'city', 'Gauteng', -25.7479, 28.2293),
    ('Cape Town', 'city', 'Western Cape', -33.9249, 18.4241),
    ('Durban', 'city', 'KwaZulu-Natal', -29.8587, 31.0218),
    ('Gqeberha', 'city', 'Eastern Cape', -33.9608, 25.6022),
    ('Gauteng', 'province', 'Gauteng', -26.2708, 28.1123),
    ('Western Cape', 'province', 'Western Cape', -33.2278, 21.8569),
    ('KwaZulu-Natal', 'province', 'KwaZulu-Natal', -28.5306, 30.8958),
    ('South Africa', 'country', null, -30.5595, 22.9375)
on conflict (canonical_name) do update
set
    location_type = excluded.location_type,
    parent_city = excluded.parent_city,
    province = excluded.province,
    latitude = excluded.latitude,
    longitude = excluded.longitude,
    source_system = excluded.source_system,
    is_verified = excluded.is_verified,
    last_seen_at = now(),
    updated_at = now();

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('johannesburg'),
        ('jozi'),
        ('joburg')
) as alias_rows(alias)
where location.canonical_name = 'Johannesburg'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('pretoria'),
        ('tshwane')
) as alias_rows(alias)
where location.canonical_name = 'Pretoria'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('cape town'),
        ('capetown'),
        ('cpt')
) as alias_rows(alias)
where location.canonical_name = 'Cape Town'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('durban'),
        ('ethekwini')
) as alias_rows(alias)
where location.canonical_name = 'Durban'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('gqeberha'),
        ('port elizabeth'),
        ('pe')
) as alias_rows(alias)
where location.canonical_name = 'Gqeberha'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('gauteng'),
        ('gp')
) as alias_rows(alias)
where location.canonical_name = 'Gauteng'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('western cape'),
        ('wc')
) as alias_rows(alias)
where location.canonical_name = 'Western Cape'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('kwazulu natal'),
        ('kwazulu-natal'),
        ('kzn')
) as alias_rows(alias)
where location.canonical_name = 'KwaZulu-Natal'
on conflict (alias) do nothing;

insert into master_location_aliases (master_location_id, alias)
select location.id, alias_rows.alias
from master_locations location
cross join lateral (
    values
        ('south africa'),
        ('za'),
        ('rsa')
) as alias_rows(alias)
where location.canonical_name = 'South Africa'
on conflict (alias) do nothing;

insert into master_industries (code, label)
values
    ('funeral_services', 'Funeral Services'),
    ('healthcare', 'Healthcare'),
    ('legal_services', 'Legal Services'),
    ('retail', 'Retail'),
    ('fitness', 'Fitness'),
    ('food_hospitality', 'Food & Hospitality'),
    ('general_services', 'General Services')
on conflict (code) do update
set
    label = excluded.label,
    updated_at = now();

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('funeral'),
        ('burial'),
        ('cremation'),
        ('memorial'),
        ('undertaker'),
        ('funeral parlour')
) as alias_rows(alias)
where industry.code = 'funeral_services'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('clinic'),
        ('dental'),
        ('hospital'),
        ('pharmacy'),
        ('healthcare')
) as alias_rows(alias)
where industry.code = 'healthcare'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('legal'),
        ('attorney'),
        ('law firm'),
        ('lawyer')
) as alias_rows(alias)
where industry.code = 'legal_services'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('retail'),
        ('grocery'),
        ('supermarket'),
        ('shop'),
        ('wholesale')
) as alias_rows(alias)
where industry.code = 'retail'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('fitness'),
        ('gym')
) as alias_rows(alias)
where industry.code = 'fitness'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('restaurant'),
        ('food'),
        ('cafe'),
        ('hospitality')
) as alias_rows(alias)
where industry.code = 'food_hospitality'
on conflict (alias) do nothing;

insert into master_languages (code, label)
values
    ('english', 'English'),
    ('afrikaans', 'Afrikaans'),
    ('isizulu', 'isiZulu'),
    ('isixhosa', 'isiXhosa'),
    ('sesotho', 'Sesotho'),
    ('sepedi', 'Sepedi'),
    ('setswana', 'Setswana'),
    ('xitsonga', 'Xitsonga'),
    ('tshivenda', 'Tshivenda'),
    ('siswati', 'Siswati'),
    ('isindebele', 'isiNdebele')
on conflict (code) do update
set
    label = excluded.label,
    updated_at = now();

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('english'),
        ('en')
) as alias_rows(alias)
where language.code = 'english'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('afrikaans'),
        ('af')
) as alias_rows(alias)
where language.code = 'afrikaans'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('isizulu'),
        ('zulu'),
        ('zu')
) as alias_rows(alias)
where language.code = 'isizulu'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('isixhosa'),
        ('xhosa'),
        ('xh')
) as alias_rows(alias)
where language.code = 'isixhosa'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('sesotho'),
        ('sotho'),
        ('st')
) as alias_rows(alias)
where language.code = 'sesotho'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('sepedi'),
        ('pedi'),
        ('nso')
) as alias_rows(alias)
where language.code = 'sepedi'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('setswana'),
        ('tswana'),
        ('tn')
) as alias_rows(alias)
where language.code = 'setswana'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('xitsonga'),
        ('tsonga'),
        ('ts')
) as alias_rows(alias)
where language.code = 'xitsonga'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('tshivenda'),
        ('venda'),
        ('ve')
) as alias_rows(alias)
where language.code = 'tshivenda'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('siswati'),
        ('swati'),
        ('ss')
) as alias_rows(alias)
where language.code = 'siswati'
on conflict (alias) do nothing;

insert into master_language_aliases (master_language_id, alias)
select language.id, alias_rows.alias
from master_languages language
cross join lateral (
    values
        ('isindebele'),
        ('ndebele'),
        ('nr')
) as alias_rows(alias)
where language.code = 'isindebele'
on conflict (alias) do nothing;

alter table leads add column if not exists latitude double precision;
alter table leads add column if not exists longitude double precision;

alter table inventory_items_final add column if not exists latitude double precision;
alter table inventory_items_final add column if not exists longitude double precision;

alter table media_outlet add column if not exists latitude double precision;
alter table media_outlet add column if not exists longitude double precision;

update inventory_items_final
set latitude = coalesce(latitude, nullif(metadata_json ->> 'latitude', '')::double precision, nullif(metadata_json ->> 'lat', '')::double precision),
    longitude = coalesce(longitude, nullif(metadata_json ->> 'longitude', '')::double precision, nullif(metadata_json ->> 'lng', '')::double precision, nullif(metadata_json ->> 'lon', '')::double precision)
where metadata_json is not null;

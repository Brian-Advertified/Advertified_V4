create table if not exists ref_language (
    code text primary key,
    label text not null,
    sort_order integer not null default 100
);

create table if not exists ref_province (
    code text primary key,
    label text not null,
    sort_order integer not null default 100
);

create table if not exists ref_broadcast_coverage_type (
    code text primary key,
    label text not null,
    sort_order integer not null default 100
);

create table if not exists ref_catalog_health (
    code text primary key,
    label text not null,
    sort_order integer not null default 100
);

insert into ref_language (code, label, sort_order) values
    ('!xuntali', '!Xuntali', 5),
    ('english', 'English', 10),
    ('afrikaans', 'Afrikaans', 20),
    ('chinyanja', 'Chinyanja', 25),
    ('french', 'French', 27),
    ('isizulu', 'isiZulu', 30),
    ('isixhosa', 'isiXhosa', 40),
    ('kiswahili', 'Kiswahili', 45),
    ('khwedam', 'Khwedam', 47),
    ('setswana', 'Setswana', 50),
    ('portuguese', 'Portuguese', 55),
    ('sesotho', 'Sesotho', 60),
    ('sepedi', 'Sepedi', 70),
    ('siswati', 'Siswati', 80),
    ('isindebele', 'isiNdebele', 90),
    ('tshivenda', 'Tshivenda', 100),
    ('xitsonga', 'Xitsonga', 110),
    ('multilingual', 'Multilingual', 120),
    ('unknown', 'Unknown', 130)
on conflict (code) do update
set label = excluded.label,
    sort_order = excluded.sort_order;

insert into ref_province (code, label, sort_order) values
    ('eastern_cape', 'Eastern Cape', 10),
    ('free_state', 'Free State', 20),
    ('gauteng', 'Gauteng', 30),
    ('kwazulu_natal', 'KwaZulu-Natal', 40),
    ('limpopo', 'Limpopo', 50),
    ('mpumalanga', 'Mpumalanga', 60),
    ('north_west', 'North West', 70),
    ('northern_cape', 'Northern Cape', 80),
    ('western_cape', 'Western Cape', 90),
    ('national', 'National', 100)
on conflict (code) do update
set label = excluded.label,
    sort_order = excluded.sort_order;

insert into ref_broadcast_coverage_type (code, label, sort_order) values
    ('local', 'Local', 10),
    ('regional', 'Regional', 20),
    ('national', 'National', 30),
    ('digital', 'Digital', 40),
    ('mixed', 'Mixed', 50),
    ('unknown', 'Unknown', 60)
on conflict (code) do update
set label = excluded.label,
    sort_order = excluded.sort_order;

insert into ref_catalog_health (code, label, sort_order) values
    ('strong', 'Strong', 10),
    ('mixed', 'Mixed', 20),
    ('mixed_not_fully_healthy', 'Mixed not fully healthy', 30),
    ('unknown', 'Unknown', 40),
    ('weak_partial_pricing', 'Weak partial pricing', 50),
    ('weak_unpriced', 'Weak unpriced', 60),
    ('weak_no_inventory', 'Weak no inventory', 70)
on conflict (code) do update
set label = excluded.label,
    sort_order = excluded.sort_order;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'media_outlet_language_language_code_fkey'
    ) then
        alter table media_outlet_language
            add constraint media_outlet_language_language_code_fkey
            foreign key (language_code) references ref_language(code);
    end if;
end $$;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'media_outlet_geography_province_code_fkey'
    ) then
        alter table media_outlet_geography
            add constraint media_outlet_geography_province_code_fkey
            foreign key (province_code) references ref_province(code);
    end if;
end $$;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'media_outlet_coverage_type_fkey'
    ) then
        alter table media_outlet
            add constraint media_outlet_coverage_type_fkey
            foreign key (coverage_type) references ref_broadcast_coverage_type(code);
    end if;
end $$;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'media_outlet_catalog_health_fkey'
    ) then
        alter table media_outlet
            add constraint media_outlet_catalog_health_fkey
            foreign key (catalog_health) references ref_catalog_health(code);
    end if;
end $$;

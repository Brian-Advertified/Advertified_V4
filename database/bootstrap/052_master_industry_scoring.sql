create table if not exists master_industry_scoring_profiles
(
    id uuid primary key default gen_random_uuid(),
    master_industry_id uuid not null unique references master_industries(id) on delete cascade,
    metadata_tag_match_score numeric(8,2) not null default 4.00,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists master_industry_media_fit_scores
(
    id uuid primary key default gen_random_uuid(),
    master_industry_scoring_profile_id uuid not null references master_industry_scoring_profiles(id) on delete cascade,
    media_type varchar(50) not null,
    score numeric(8,2) not null,
    created_at timestamptz not null default now(),
    unique(master_industry_scoring_profile_id, media_type)
);

create index if not exists ix_master_industry_media_fit_scores_profile_id
    on master_industry_media_fit_scores(master_industry_scoring_profile_id);

create table if not exists master_industry_audience_hint_scores
(
    id uuid primary key default gen_random_uuid(),
    master_industry_scoring_profile_id uuid not null references master_industry_scoring_profiles(id) on delete cascade,
    hint_token varchar(100) not null,
    score numeric(8,2) not null,
    created_at timestamptz not null default now(),
    unique(master_industry_scoring_profile_id, hint_token)
);

create index if not exists ix_master_industry_audience_hint_scores_profile_id
    on master_industry_audience_hint_scores(master_industry_scoring_profile_id);

insert into master_industries (code, label)
values ('automotive', 'Automotive')
on conflict (code) do update
set label = excluded.label;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_value.alias
from master_industries industry
cross join (values
    ('automotive'),
    ('auto'),
    ('car'),
    ('cars'),
    ('dealer'),
    ('dealership'),
    ('vehicle'),
    ('motor')
) as alias_value(alias)
where industry.code = 'automotive'
on conflict do nothing;

insert into master_industry_scoring_profiles (master_industry_id, metadata_tag_match_score)
select industry.id, 4.00
from master_industries industry
where industry.code in ('funeral_services', 'food_hospitality', 'retail', 'healthcare', 'legal_services', 'automotive')
on conflict (master_industry_id) do update
set metadata_tag_match_score = excluded.metadata_tag_match_score,
    updated_at = now();

insert into master_industry_media_fit_scores (master_industry_scoring_profile_id, media_type, score)
select profile.id, seeded.media_type, seeded.score
from master_industry_scoring_profiles profile
join master_industries industry on industry.id = profile.master_industry_id
join (
    values
        ('funeral_services', 'radio', 6.00),
        ('funeral_services', 'ooh', 4.00),
        ('funeral_services', 'digital', 1.00),
        ('funeral_services', 'tv', 0.00),
        ('food_hospitality', 'digital', 6.00),
        ('food_hospitality', 'ooh', 5.00),
        ('food_hospitality', 'radio', 3.00),
        ('food_hospitality', 'tv', 0.00),
        ('retail', 'radio', 5.00),
        ('retail', 'ooh', 5.00),
        ('retail', 'digital', 4.00),
        ('retail', 'tv', 1.00),
        ('healthcare', 'search', 6.00),
        ('healthcare', 'digital', 5.00),
        ('healthcare', 'radio', 3.00),
        ('healthcare', 'ooh', 2.00),
        ('legal_services', 'radio', 4.00),
        ('legal_services', 'digital', 4.00),
        ('legal_services', 'ooh', 3.00),
        ('automotive', 'radio', 5.00),
        ('automotive', 'ooh', 5.00),
        ('automotive', 'digital', 4.00),
        ('automotive', 'tv', 2.00)
) as seeded(industry_code, media_type, score) on seeded.industry_code = industry.code
on conflict (master_industry_scoring_profile_id, media_type) do update
set score = excluded.score;

insert into master_industry_audience_hint_scores (master_industry_scoring_profile_id, hint_token, score)
select profile.id, seeded.hint_token, seeded.score
from master_industry_scoring_profiles profile
join master_industries industry on industry.id = profile.master_industry_id
join (
    values
        ('funeral_services', 'news', 3.00),
        ('funeral_services', 'current affairs', 3.00),
        ('funeral_services', 'trust', 3.00),
        ('funeral_services', 'family', 3.00),
        ('funeral_services', 'older', 3.00),
        ('food_hospitality', 'commuter', 3.00),
        ('food_hospitality', 'impulse', 3.00),
        ('food_hospitality', 'shopping', 3.00),
        ('food_hospitality', 'lifestyle', 3.00),
        ('retail', 'shopping', 3.00),
        ('retail', 'retail', 3.00),
        ('retail', 'mass market', 3.00),
        ('retail', 'family', 3.00),
        ('healthcare', 'health', 3.00),
        ('healthcare', 'wellness', 3.00),
        ('healthcare', 'family', 3.00),
        ('healthcare', 'local services', 3.00),
        ('legal_services', 'professional', 2.00),
        ('legal_services', 'adult', 2.00),
        ('legal_services', 'trust', 2.00),
        ('automotive', 'commuter', 3.00),
        ('automotive', 'drivers', 3.00),
        ('automotive', 'mass market', 3.00)
) as seeded(industry_code, hint_token, score) on seeded.industry_code = industry.code
on conflict (master_industry_scoring_profile_id, hint_token) do update
set score = excluded.score;

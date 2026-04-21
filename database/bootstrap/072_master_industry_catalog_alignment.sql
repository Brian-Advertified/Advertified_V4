insert into master_industries (code, label)
values
    ('finance', 'Finance'),
    ('real_estate', 'Real Estate'),
    ('technology', 'Technology')
on conflict (code) do update
set
    label = excluded.label,
    updated_at = now();

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('finance'),
        ('financial'),
        ('financial services'),
        ('bank'),
        ('banking'),
        ('insurance')
) as alias_rows(alias)
where industry.code = 'finance'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('health'),
        ('medical')
) as alias_rows(alias)
where industry.code = 'healthcare'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('real estate'),
        ('realestate'),
        ('property'),
        ('property sales'),
        ('estate agent'),
        ('realtor'),
        ('realty')
) as alias_rows(alias)
where industry.code = 'real_estate'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('technology'),
        ('tech'),
        ('software'),
        ('saas'),
        ('it'),
        ('information technology')
) as alias_rows(alias)
where industry.code = 'technology'
on conflict (alias) do nothing;

insert into master_industry_aliases (master_industry_id, alias)
select industry.id, alias_rows.alias
from master_industries industry
cross join lateral (
    values
        ('general'),
        ('general services'),
        ('other')
) as alias_rows(alias)
where industry.code = 'general_services'
on conflict (alias) do nothing;

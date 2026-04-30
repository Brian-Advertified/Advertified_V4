-- Imports the legacy industry CSV seed into the current V4 industry intelligence tables.
-- Keep this as data mapping only; runtime behavior stays in the existing industry services.

create temp table if not exists tmp_industry_seed_import
(
    source_code varchar(80),
    code varchar(80),
    label varchar(160),
    aliases text,
    primary_audience text,
    demographics text,
    income_group varchar(40),
    coverage_type text,
    objective_primary text,
    objective_secondary text,
    primary_tags text,
    secondary_tags text,
    notes text,
    default_objective varchar(80),
    funnel_shape varchar(80),
    preferred_tone varchar(80),
    preferred_channels_json jsonb,
    base_budget_split_json jsonb,
    cta text,
    geography_bias varchar(80),
    restricted_claim_types_json jsonb,
    sort_order integer
) on commit drop;

truncate table tmp_industry_seed_import;

insert into tmp_industry_seed_import (
    source_code,
    code,
    label,
    aliases,
    primary_audience,
    demographics,
    income_group,
    coverage_type,
    objective_primary,
    objective_secondary,
    primary_tags,
    secondary_tags,
    notes,
    default_objective,
    funnel_shape,
    preferred_tone,
    preferred_channels_json,
    base_budget_split_json,
    cta,
    geography_bias,
    restricted_claim_types_json,
    sort_order
)
values
    ('retail', 'retail', 'Retail & Ecommerce', 'retail ecommerce|ecommerce|e-commerce|online retail|retail and ecommerce|retail', 'Online shoppers and household buyers', 'Deal-seeking, mobile-first and household purchase oriented', 'Mid', 'Broad national + metro weighting', 'Sales / Conversions', 'Awareness / Promotions', 'online shoppers, deal seekers, fashion, beauty, household buyers, mobile-first consumers', 'parents, aspirational middle income, card users, cart abandoners, festive buyers', 'Best for promo-led, seasonal and conversion-heavy proposals; validate category-specific gender skew with client data.', 'promotion', 'promotion-led', 'performance', '["Digital", "OOH", "Radio", "Newspaper"]'::jsonb, '{"Digital": 40, "OOH": 25, "Radio": 25, "Newspaper": 10}'::jsonb, 'Visit us today', 'national-metro-weighted', '[]'::jsonb, 110),
    ('fmcg', 'fmcg', 'FMCG / Consumer Packaged Goods', 'consumer packaged goods|cpg|fast moving consumer goods|fmcg|grocery|consumer goods', 'Mass market shoppers and families', 'Broad-reach, price-sensitive urban and township consumers', 'Mid', 'Broad national reach', 'Awareness / Reach', 'Sales Uplift / Store Visits', 'mass market shoppers, grocery buyers, families, township consumers, urban consumers, price-sensitive buyers', 'youth snackers, parents, convenience seekers, promotion responders, weekend shoppers', 'Good for broad coverage and frequency; strongest when paired with retailer promotions or regional price messaging.', 'awareness', 'reach-led', 'high-visibility', '["Radio", "OOH", "Digital", "Newspaper"]'::jsonb, '{"Radio": 35, "OOH": 30, "Digital": 25, "Newspaper": 10}'::jsonb, 'Find it in store', 'national-reach', '[]'::jsonb, 120),
    ('finance', 'finance', 'Financial Services / Banking', 'financial services|banking|bank|finserv|financial services banking|finance', 'Salary earners, professionals and SME owners', 'Trust-driven, consideration-heavy financial audiences', 'High', 'National + metro weighting', 'Leads / Account Acquisition', 'Consideration / Education', 'salary earners, credit-active consumers, affluent professionals, SME owners, home loan prospects', 'young professionals, family planners, investors, premium card users, high LSM audiences', 'Trust and clarity matter more than discount messaging; ideal for problem-solution proposal framing.', 'leads', 'consideration-led', 'high-trust', '["Digital", "Newspaper", "Radio"]'::jsonb, '{"Digital": 45, "Newspaper": 30, "Radio": 20, "OOH": 5}'::jsonb, 'Request a consultation', 'national-metro-weighted', '["financial returns", "credit approval guarantees", "unverified savings claims"]'::jsonb, 130),
    ('insurance', 'insurance', 'Insurance', 'cover|life cover|medical aid|short term insurance|insurance|policy seekers', 'Parents, homeowners and policy seekers', 'Risk-aware households and value-protection buyers', 'High', 'National + metro weighting', 'Leads / Quote Requests', 'Education / Consideration', 'policy seekers, vehicle owners, homeowners, parents, income protectors', 'medical aid shoppers, life cover prospects, business owners, risk-aware households, premium buyers', 'Strong need-state category; messaging should reduce uncertainty and explain value, not just price.', 'leads', 'consideration-led', 'high-trust', '["Digital", "Newspaper", "Radio", "OOH"]'::jsonb, '{"Digital": 40, "Newspaper": 25, "Radio": 20, "OOH": 15}'::jsonb, 'Request a quote', 'national-metro-weighted', '["guaranteed cover", "financial returns", "unverified savings claims"]'::jsonb, 140),
    ('automotive', 'automotive', 'Automotive', 'auto|cars|vehicles|dealership|automotive|vehicle dealers', 'Vehicle intenders and commuters', 'Aspiration-led but utility-driven vehicle buyers', 'MidHigh', 'National + metro + affluent suburb weighting', 'Leads / Test Drives', 'Awareness / Dealer Visits', 'vehicle intenders, car owners, commuters, upgrade seekers, dealership prospects', 'SUV families, bakkie buyers, premium auto shoppers, service-plan audiences, motorsport fans', 'Weekend and drive-time messaging matter; creative should connect vehicle choice to lifestyle or utility.', 'leads', 'intent-led', 'performance', '["Digital", "Radio", "OOH", "Newspaper"]'::jsonb, '{"Digital": 35, "Radio": 30, "OOH": 25, "Newspaper": 10}'::jsonb, 'Book a test drive', 'metro-suburb-weighted', '["guaranteed finance approval", "unverified savings claims"]'::jsonb, 150),
    ('real_estate', 'real_estate', 'Real Estate / Property', 'property|real estate|housing|estate agency|estate agents|realty', 'Home buyers, renters and property investors', 'Geography-led, family and household decision maker audience', 'High', 'Metro + suburb clusters', 'Leads / Viewing Bookings', 'Awareness / Listing Promotion', 'home buyers, renters, property investors, relocating families, mortgage-ready households', 'affluent suburb movers, first-time buyers, sectional-title seekers, estate living, downsizers', 'Local geography matters more than broad reach; weekends often outperform for inquiry and viewing intent.', 'leads', 'local-intent-led', 'high-trust', '["Digital", "Newspaper", "OOH"]'::jsonb, '{"Digital": 45, "Newspaper": 30, "OOH": 20, "Radio": 5}'::jsonb, 'Book a viewing', 'local-first', '["investment guarantees", "unverified property value claims"]'::jsonb, 160),
    ('education', 'education', 'Education & Training', 'education|training|college|university|learning|courses|study', 'School leavers, students and upskill seekers', 'Aspirational and employability-driven learner audience', 'Mid', 'National + metro weighting', 'Leads / Applications', 'Awareness / Open Days', 'school leavers, students, upskill seekers, job switchers, bursary hunters', 'parents, adult learners, distance learners, TVET prospects, postgraduate applicants', 'Often dual-audience: youth plus parents or guardians; proposals should reflect both aspiration and employability.', 'leads', 'application-led', 'balanced', '["Digital", "Radio", "Newspaper", "OOH"]'::jsonb, '{"Digital": 50, "Radio": 20, "Newspaper": 15, "OOH": 15}'::jsonb, 'Request information', 'national-metro-weighted', '["guaranteed employment", "unverified accreditation claims"]'::jsonb, 170),
    ('healthcare', 'healthcare', 'Healthcare / Medical / Pharmacy', 'healthcare|medical|pharmacy|clinic|hospital|doctor|medical practice', 'Parents, caregivers and appointment bookers', 'Trust-sensitive family health and chronic care audiences', 'MidHigh', 'Local catchment + metro', 'Leads / Appointments', 'Education / Awareness', 'parents, caregivers, chronic care audiences, pharmacy shoppers, appointment bookers', 'wellness seekers, medical aid members, women health, family health decision makers, older adults', 'Use careful compliance-safe language; geographic relevance and trust signals are critical.', 'leads', 'trust-led', 'high-trust', '["Digital", "Newspaper", "Radio"]'::jsonb, '{"Digital": 45, "Newspaper": 30, "Radio": 20, "OOH": 5}'::jsonb, 'Book an appointment', 'local-first', '["medical guarantees", "cure claims", "unverified clinical outcomes"]'::jsonb, 180),
    ('telecoms', 'telecoms', 'Telecoms & Connectivity', 'telecoms|connectivity|mobile network|telco|internet|fibre|data', 'Data buyers, prepaid users and fibre prospects', 'High-frequency mobile-first connected consumers', 'Mid', 'Broad national reach', 'Sales / Acquisition', 'Awareness / Upsell', 'data buyers, prepaid users, smartphone upgraders, fibre prospects, streaming-heavy users', 'students, gamers, young professionals, households comparing packages, SIM switchers', 'Mobile access dominates in South Africa, so this vertical suits strong mobile-first creative and offers.', 'promotion', 'acquisition-led', 'performance', '["Digital", "Radio", "OOH"]'::jsonb, '{"Digital": 50, "Radio": 25, "OOH": 20, "Newspaper": 5}'::jsonb, 'Check coverage today', 'national-reach', '["coverage guarantees", "unverified speed claims"]'::jsonb, 190),
    ('technology', 'technology', 'Technology / SaaS / B2B Digital Services', 'technology|saas|software|b2b digital services|tech|digital services', 'IT managers, founders and operations leads', 'Business problem-solvers and procurement influencers', 'High', 'National + metro business hubs', 'Leads / Demo Requests', 'Consideration / Thought Leadership', 'IT managers, founders, operations leads, SME owners, procurement influencers', 'finance leads, HR tech buyers, digital transformation teams, CIO-level audiences, startup operators', 'Best framed around efficiency, cost control and business problems solved rather than media inventory.', 'leads', 'b2b-consideration-led', 'high-trust', '["Digital", "Newspaper", "Radio"]'::jsonb, '{"Digital": 65, "Newspaper": 20, "Radio": 10, "OOH": 5}'::jsonb, 'Book a demo', 'metro-business-weighted', '["unverified performance claims", "security guarantees"]'::jsonb, 200),
    ('tourism', 'travel', 'Travel / Tourism / Hospitality', 'travel|tourism|hospitality|hotels|accommodation|tour operators|holiday packages', 'Holiday planners and leisure travellers', 'Aspirational domestic and regional travel planners', 'MidHigh', 'National + regional origin markets', 'Bookings / Conversions', 'Awareness / Lead Capture', 'holiday planners, domestic travellers, family trip planners, couples, premium leisure seekers', 'weekend escape audiences, SADC travellers, adventure seekers, wedding travellers, loyalty members', 'Tourism recovered strongly in 2025; proposals should connect spend to bookings, occupancy or package sales.', 'leads', 'booking-led', 'aspirational', '["Digital", "OOH", "Radio", "Newspaper"]'::jsonb, '{"Digital": 50, "OOH": 20, "Radio": 15, "Newspaper": 15}'::jsonb, 'Plan your trip', 'regional-origin-weighted', '["availability guarantees", "unverified price claims"]'::jsonb, 210),
    ('restaurant', 'food_hospitality', 'QSR / Restaurants / Food Delivery', 'food|qsr|restaurants|food delivery|takeaways|restaurant|quick service restaurant|delivery food', 'Quick meal buyers and app-order users', 'Impulse-led, convenience-driven local diners', 'Mid', 'Hyperlocal + metro radius', 'Sales / Orders', 'Store Visits / Awareness', 'quick meal buyers, students, office lunch buyers, late-night snackers, app-order users', 'families, commuters, value-menu shoppers, sports viewers, delivery-first consumers', 'Dayparting is critical; tie messaging to hunger moments, value bundles and location proximity.', 'foot_traffic', 'daypart-led', 'performance', '["Digital", "OOH", "Radio"]'::jsonb, '{"Digital": 55, "OOH": 30, "Radio": 10, "Newspaper": 5}'::jsonb, 'Order or visit today', 'hyperlocal', '["nutrition claims", "unverified price claims"]'::jsonb, 220),
    ('home_improvement', 'home_improvement', 'Home Improvement / Furniture / Appliances', 'home improvement|furniture|appliances|renovation|diy|hardware|homeware', 'Home improvers and household shoppers', 'Family-led home upgrade and practical purchase audience', 'MidHigh', 'Metro + regional retail nodes', 'Sales / Store Visits', 'Leads / Catalogue Engagement', 'home improvers, renovators, new movers, appliance buyers, furniture shoppers', 'credit shoppers, family households, DIY audiences, premium home buyers, payday responders', 'Often correlates with household decision makers and payday periods; creative should show practical benefit.', 'promotion', 'retail-intent-led', 'balanced', '["Digital", "OOH", "Newspaper", "Radio"]'::jsonb, '{"Digital": 40, "OOH": 25, "Newspaper": 25, "Radio": 10}'::jsonb, 'View the latest offers', 'regional-retail-weighted', '["unverified finance approval", "unverified savings claims"]'::jsonb, 230),
    ('logistics', 'logistics', 'Logistics / Courier / Delivery', 'logistics|courier|delivery|shipping|freight|parcel delivery', 'SME shippers and operations decision makers', 'Reliability-driven B2B and ecommerce fulfilment audience', 'MidHigh', 'National + regional corridors', 'Leads / Account Acquisition', 'Awareness / Service Education', 'SME shippers, ecommerce operators, supply chain managers, fleet owners, dispatch decision makers', 'cross-border traders, regional wholesalers, parcel senders, procurement teams, operations heads', 'Best used as a B2B utility proposition; reliability and turnaround-time claims matter more than reach alone.', 'leads', 'b2b-utility-led', 'high-trust', '["Digital", "Radio", "Newspaper", "OOH"]'::jsonb, '{"Digital": 45, "Radio": 25, "Newspaper": 20, "OOH": 10}'::jsonb, 'Request a business quote', 'corridor-weighted', '["guaranteed delivery times", "unverified coverage claims"]'::jsonb, 240),
    ('recruitment', 'recruitment', 'Recruitment / Jobs / HR Services', 'jobs|recruitment|hr|employment|talent|careers|staffing', 'Job seekers and HR decision makers', 'Opportunity-led job, talent and employer-brand audience', 'Mid', 'National + metro weighting', 'Leads / Applications', 'Awareness / Talent Brand', 'job seekers, graduates, career switchers, skilled professionals, unemployed youth', 'recruiters, HR managers, contract workers, gig seekers, learn-and-earn audiences', 'Useful for both candidate acquisition and employer branding; creative should be practical and opportunity-led.', 'leads', 'application-led', 'balanced', '["Digital", "Newspaper", "Radio"]'::jsonb, '{"Digital": 50, "Newspaper": 30, "Radio": 15, "OOH": 5}'::jsonb, 'Apply or enquire today', 'national-metro-weighted', '["guaranteed placement", "unverified salary claims"]'::jsonb, 250),
    ('agriculture', 'agriculture', 'Agriculture / Agri-inputs / Equipment', 'agriculture|agri|farming|agri inputs|equipment|farm equipment|farmers', 'Farm owners and agri-input buyers', 'Regional, seasonal and business-oriented agricultural audience', 'MidHigh', 'Regional + rural / agri belts', 'Leads / Dealer Enquiries', 'Awareness / Event Attendance', 'farm owners, input buyers, equipment operators, agri SMEs, co-op members', 'livestock producers, crop farmers, rural entrepreneurs, irrigation buyers, seasonal finance prospects', 'More niche than broad consumer sectors; geography and seasonality should drive targeting presets.', 'leads', 'regional-utility-led', 'high-trust', '["Radio", "Newspaper", "Digital", "OOH"]'::jsonb, '{"Radio": 35, "Newspaper": 30, "Digital": 25, "OOH": 10}'::jsonb, 'Speak to a dealer', 'regional-rural-weighted', '["yield guarantees", "unverified finance claims"]'::jsonb, 260);

insert into master_industries (code, label)
select distinct code, label
from tmp_industry_seed_import
on conflict (code) do update
set
    label = excluded.label,
    updated_at = now();

delete from master_industry_aliases alias
using master_industries industry
where alias.master_industry_id = industry.id
  and industry.code <> 'insurance'
  and lower(alias.alias) in ('insurance', 'cover', 'life cover', 'medical aid', 'short term insurance');

insert into master_industry_aliases (master_industry_id, alias)
select distinct industry.id, lower(trim(alias_value.alias))
from tmp_industry_seed_import seeded
join master_industries industry on industry.code = seeded.code
cross join lateral regexp_split_to_table(seeded.aliases, '\|') as alias_value(alias)
where trim(alias_value.alias) <> ''
on conflict (alias) do update
set master_industry_id = excluded.master_industry_id
where master_industry_aliases.alias in ('insurance', 'cover', 'life cover', 'medical aid', 'short term insurance');

insert into lead_industry_policies (
    key,
    name,
    objective_override,
    preferred_tone,
    preferred_channels_json,
    cta,
    messaging_angle,
    guardrails_json,
    additional_gap,
    additional_outcome,
    sort_order,
    is_active
)
select
    code,
    label,
    default_objective,
    preferred_tone,
    preferred_channels_json,
    cta,
    demographics,
    jsonb_build_array(
        'Keep claims accurate and easy to verify.',
        'Match creative to audience, geography, and buying moment.',
        'Use clear next-step CTA language.'
    ) || restricted_claim_types_json,
    'Imported legacy industry seed improves default audience, channel, and objective fit for ' || lower(label) || '.',
    'Planner can bias recommendations toward the channels, audience hints, and lead actions that fit this industry.',
    sort_order,
    true
from tmp_industry_seed_import
on conflict (key) do update
set
    name = excluded.name,
    objective_override = excluded.objective_override,
    preferred_tone = excluded.preferred_tone,
    preferred_channels_json = excluded.preferred_channels_json,
    cta = excluded.cta,
    messaging_angle = excluded.messaging_angle,
    guardrails_json = excluded.guardrails_json,
    additional_gap = excluded.additional_gap,
    additional_outcome = excluded.additional_outcome,
    sort_order = excluded.sort_order,
    is_active = excluded.is_active,
    updated_at = now();

insert into master_industry_strategy_profiles (
    master_industry_id,
    primary_persona,
    buying_journey,
    trust_sensitivity,
    default_language_biases_json,
    default_objective,
    funnel_shape,
    primary_kpis_json,
    sales_cycle,
    preferred_channels_json,
    base_budget_split_json,
    geography_bias,
    preferred_tone,
    messaging_angle,
    recommended_cta,
    proof_points_json,
    guardrails_json,
    restricted_claim_types_json,
    research_summary,
    research_sources_json
)
select
    industry.id,
    seeded.primary_audience,
    seeded.demographics,
    case
        when seeded.income_group ilike '%high%' or seeded.preferred_tone = 'high-trust' then 'high'
        when seeded.preferred_tone = 'balanced' then 'medium'
        else 'medium-high'
    end,
    '["English"]'::jsonb,
    seeded.default_objective,
    seeded.funnel_shape,
    jsonb_build_array(seeded.objective_primary, seeded.objective_secondary),
    case
        when seeded.default_objective in ('leads') then 'considered'
        when seeded.default_objective in ('awareness') then 'frequency-led'
        else 'short-response'
    end,
    seeded.preferred_channels_json,
    seeded.base_budget_split_json,
    seeded.geography_bias,
    seeded.preferred_tone,
    seeded.demographics,
    seeded.cta,
    jsonb_build_array(seeded.primary_tags, seeded.secondary_tags, seeded.notes),
    jsonb_build_array(
        'Keep claims accurate and easy to verify.',
        'Match targeting to the audience segment and geography.',
        'Use channel mix based on buying journey rather than reach alone.'
    ),
    seeded.restricted_claim_types_json,
    seeded.notes,
    '[]'::jsonb
from tmp_industry_seed_import seeded
join master_industries industry on industry.code = seeded.code
on conflict (master_industry_id) do nothing;

insert into master_industry_scoring_profiles (master_industry_id, metadata_tag_match_score)
select industry.id, 4.50
from tmp_industry_seed_import seeded
join master_industries industry on industry.code = seeded.code
on conflict (master_industry_id) do update
set
    metadata_tag_match_score = greatest(master_industry_scoring_profiles.metadata_tag_match_score, excluded.metadata_tag_match_score),
    updated_at = now();

insert into master_industry_media_fit_scores (master_industry_scoring_profile_id, media_type, score)
select profile.id, media_scores.media_type, media_scores.score
from tmp_industry_seed_import seeded
join master_industries industry on industry.code = seeded.code
join master_industry_scoring_profiles profile on profile.master_industry_id = industry.id
join lateral (
    values
        ('digital', case seeded.code
            when 'retail' then 4.00 when 'fmcg' then 4.00 when 'finance' then 6.00 when 'insurance' then 5.00
            when 'automotive' then 4.00 when 'real_estate' then 6.00 when 'education' then 6.00 when 'healthcare' then 5.00
            when 'telecoms' then 6.00 when 'technology' then 6.00 when 'travel' then 6.00 when 'food_hospitality' then 6.00
            when 'home_improvement' then 5.00 when 'logistics' then 5.00 when 'recruitment' then 5.00 when 'agriculture' then 3.00
            else 4.00 end),
        ('radio', case seeded.code
            when 'retail' then 5.00 when 'fmcg' then 5.00 when 'finance' then 4.00 when 'insurance' then 4.00
            when 'automotive' then 5.00 when 'real_estate' then 2.00 when 'education' then 4.00 when 'healthcare' then 3.00
            when 'telecoms' then 4.00 when 'technology' then 2.00 when 'travel' then 3.00 when 'food_hospitality' then 3.00
            when 'home_improvement' then 3.00 when 'logistics' then 4.00 when 'recruitment' then 3.00 when 'agriculture' then 4.00
            else 3.00 end),
        ('ooh', case seeded.code
            when 'retail' then 5.00 when 'fmcg' then 5.00 when 'finance' then 2.00 when 'insurance' then 3.00
            when 'automotive' then 5.00 when 'real_estate' then 4.00 when 'education' then 3.00 when 'healthcare' then 2.00
            when 'telecoms' then 4.00 when 'technology' then 1.00 when 'travel' then 4.00 when 'food_hospitality' then 5.00
            when 'home_improvement' then 4.00 when 'logistics' then 3.00 when 'recruitment' then 2.00 when 'agriculture' then 2.00
            else 3.00 end),
        ('tv', case seeded.code
            when 'retail' then 1.00 when 'fmcg' then 3.00 when 'finance' then 2.00 when 'insurance' then 2.00
            when 'automotive' then 2.00 when 'real_estate' then 1.00 when 'education' then 1.00 when 'healthcare' then 1.00
            when 'telecoms' then 2.00 when 'technology' then 0.00 when 'travel' then 2.00 when 'food_hospitality' then 0.00
            when 'home_improvement' then 1.00 when 'logistics' then 0.00 when 'recruitment' then 0.00 when 'agriculture' then 1.00
            else 1.00 end),
        ('newspaper', case seeded.code
            when 'retail' then 3.00 when 'fmcg' then 3.00 when 'finance' then 5.00 when 'insurance' then 4.00
            when 'automotive' then 3.00 when 'real_estate' then 4.00 when 'education' then 3.00 when 'healthcare' then 4.00
            when 'telecoms' then 1.00 when 'technology' then 3.00 when 'travel' then 3.00 when 'food_hospitality' then 2.00
            when 'home_improvement' then 4.00 when 'logistics' then 3.00 when 'recruitment' then 4.00 when 'agriculture' then 4.00
            else 2.00 end)
) as media_scores(media_type, score) on true
on conflict (master_industry_scoring_profile_id, media_type) do update
set score = excluded.score;

insert into master_industry_audience_hint_scores (master_industry_scoring_profile_id, hint_token, score)
select hint_rows.profile_id, hint_rows.hint_token, max(hint_rows.score)
from (
    select profile.id as profile_id, lower(trim(hint_value.hint)) as hint_token, hint_value.score
    from tmp_industry_seed_import seeded
    join master_industries industry on industry.code = seeded.code
    join master_industry_scoring_profiles profile on profile.master_industry_id = industry.id
    cross join lateral (
        select hint, 3.00::numeric as score
        from regexp_split_to_table(seeded.primary_tags, ',') as primary_hint(hint)
        union all
        select hint, 2.00::numeric as score
        from regexp_split_to_table(seeded.secondary_tags, ',') as secondary_hint(hint)
    ) as hint_value
    where trim(hint_value.hint) <> ''
) as hint_rows
group by hint_rows.profile_id, hint_rows.hint_token
on conflict (master_industry_scoring_profile_id, hint_token) do update
set score = greatest(master_industry_audience_hint_scores.score, excluded.score);

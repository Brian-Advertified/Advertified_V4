create table if not exists planning_engine_settings
(
    setting_key varchar(120) primary key,
    setting_value text not null,
    description text null,
    updated_at timestamptz not null default now()
);

insert into planning_engine_settings (setting_key, setting_value, description)
values
    ('brief_intent_local_ooh_min_dimension_matches', '2', 'Minimum brief-intent dimensions a local OOH candidate must satisfy before selection.'),
    ('brief_intent_local_ooh_radius_km', '20', 'Primary local OOH radius from the resolved main-area coordinates.'),
    ('brief_intent_relaxed_local_ooh_radius_km', '35', 'Fallback local OOH radius when geography is relaxed during a later planning pass.'),
    ('brief_intent_score_per_match', '4', 'Score bonus per matched brief-intent dimension across candidate evaluation.'),
    ('brief_intent_full_match_bonus', '4', 'Extra score bonus when every considered brief-intent dimension is satisfied.'),
    ('brief_intent_require_local_ooh_audience_evidence', 'true', 'Require local OOH candidates to carry audience/context metadata when a rich brief is provided.')
on conflict (setting_key) do update
set
    setting_value = excluded.setting_value,
    description = excluded.description,
    updated_at = now();

create table if not exists lead_intelligence_settings
(
    setting_key varchar(120) primary key,
    setting_value text not null,
    description text null,
    updated_at timestamptz not null default now()
);

insert into lead_intelligence_settings (setting_key, setting_value, description)
values
    ('scoring_base_score', '0', 'Base lead intent score applied before activity and opportunity adjustments.'),
    ('scoring_activity_promo_active', '15', 'Score bonus when promotional activity is detected.'),
    ('scoring_activity_meta_strong', '20', 'Score bonus when social/meta activity is strong.'),
    ('scoring_activity_website_active', '10', 'Score bonus when the website shows recent activity.'),
    ('scoring_activity_multi_channel_presence', '5', 'Score bonus when multiple active channels are detected.'),
    ('scoring_opportunity_digital_strong_but_search_weak', '15', 'Opportunity score bonus when digital is strong but search capture is weak.'),
    ('scoring_opportunity_digital_strong_but_ooh_weak', '15', 'Opportunity score bonus when digital is strong but OOH is weak.'),
    ('scoring_opportunity_promo_heavy_but_brand_presence_weak', '10', 'Opportunity score bonus when promotions are visible but broad-reach presence is weak.'),
    ('scoring_opportunity_single_channel_dependency', '10', 'Opportunity score bonus when only one active channel is detected.'),
    ('scoring_threshold_strong_channel_min', '60', 'Minimum channel score considered strong.'),
    ('scoring_threshold_weak_channel_max', '39', 'Maximum channel score still treated as weak.'),
    ('scoring_threshold_active_channel_min', '40', 'Minimum channel score considered active.'),
    ('scoring_intent_low_max', '40', 'Maximum score still classified as low intent.'),
    ('scoring_intent_medium_max', '70', 'Maximum score still classified as medium intent.'),
    ('automation_enabled', 'false', 'Enable scheduled lead intelligence refresh processing.'),
    ('automation_refresh_interval_minutes', '60', 'Minutes between scheduled lead intelligence refresh passes.'),
    ('automation_batch_size', '100', 'Maximum number of leads processed per scheduled batch.'),
    ('automation_run_on_startup', 'false', 'Run the lead intelligence jobs once when the API starts.'),
    ('automation_enable_paid_media_evidence_sync', 'false', 'Enable scheduled paid media evidence sync jobs.'),
    ('automation_paid_media_sync_interval_minutes', '180', 'Minutes between paid media evidence sync runs.')
on conflict (setting_key) do update
set
    setting_value = excluded.setting_value,
    description = excluded.description,
    updated_at = now();

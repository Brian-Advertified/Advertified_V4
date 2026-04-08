INSERT INTO media_outlet (
    id,
    code,
    name,
    media_type,
    coverage_type,
    catalog_health,
    operator_name,
    is_national,
    has_pricing,
    language_notes,
    audience_age_skew,
    audience_gender_skew,
    audience_lsm_range,
    target_audience,
    data_source_enrichment,
    strategy_fit_json,
    created_at,
    updated_at
)
VALUES
    (
        '84f9fef4-2e11-4f48-a9fb-7e6c9cf9d101',
        'social_meta',
        'Meta (Facebook + Instagram)',
        'digital',
        'national',
        'strong',
        'Meta',
        true,
        true,
        'Auction-based social inventory covering Facebook and Instagram placements.',
        '25-54',
        'all',
        'LSM 4-10',
        'Mass-market B2C reach',
        '{"platform":"meta","family":"social_media","billing_model":"auction","inventory_kind":"benchmark","source_snapshot_date":"2026-04-07"}'::jsonb::text,
        '{"buying_behaviour_fit":"price_sensitive,brand_conscious,convenience_driven","price_positioning_fit":"budget,mid_range,premium","sales_model_fit":"online_sales,walk_ins,hybrid","objective_fit_primary":"awareness","objective_fit_secondary":"lead_generation","environment_type":"social_feed","premium_mass_fit":"mass_market,mid_market","data_confidence":"medium","intelligence_notes":"Best used for broad B2C reach, prospecting, retargeting, and mobile-first campaign support."}'::jsonb,
        now(),
        now()
    ),
    (
        '84f9fef4-2e11-4f48-a9fb-7e6c9cf9d102',
        'social_tiktok',
        'TikTok',
        'digital',
        'national',
        'strong',
        'TikTok',
        true,
        true,
        'Auction-based short-form video inventory.',
        '18-34',
        'all',
        'LSM 4-8',
        'Discovery-led B2C audiences',
        '{"platform":"tiktok","family":"social_media","billing_model":"auction","inventory_kind":"benchmark","source_snapshot_date":"2026-04-07"}'::jsonb::text,
        '{"buying_behaviour_fit":"impulse,brand_conscious,convenience_driven","price_positioning_fit":"budget,mid_range","sales_model_fit":"online_sales,hybrid","objective_fit_primary":"awareness","objective_fit_secondary":"video_views","environment_type":"short_form_video","premium_mass_fit":"mass_market","data_confidence":"medium","intelligence_notes":"Strong fit for short-form video discovery, youth attention, and momentum-building campaigns."}'::jsonb,
        now(),
        now()
    ),
    (
        '84f9fef4-2e11-4f48-a9fb-7e6c9cf9d103',
        'social_youtube',
        'YouTube',
        'digital',
        'national',
        'strong',
        'Google Ads',
        true,
        true,
        'Auction-based video inventory through Google Ads.',
        '25-54',
        'all',
        'LSM 5-10',
        'Scaled video awareness audiences',
        '{"platform":"youtube","family":"social_media","billing_model":"auction","inventory_kind":"benchmark","source_snapshot_date":"2026-04-07"}'::jsonb::text,
        '{"buying_behaviour_fit":"quality_focused,brand_conscious,convenience_driven","price_positioning_fit":"mid_range,premium","sales_model_fit":"online_sales,hybrid,direct_sales","objective_fit_primary":"awareness","objective_fit_secondary":"video_views","environment_type":"long_form_video","premium_mass_fit":"mass_market,premium","data_confidence":"medium","intelligence_notes":"Best used for scaled video awareness, storytelling, and higher-consideration video support."}'::jsonb,
        now(),
        now()
    ),
    (
        '84f9fef4-2e11-4f48-a9fb-7e6c9cf9d104',
        'social_linkedin',
        'LinkedIn',
        'digital',
        'national',
        'strong',
        'LinkedIn',
        true,
        true,
        'Auction-based professional social inventory.',
        '25-54',
        'all',
        'LSM 7-10',
        'Professional and B2B decision-makers',
        '{"platform":"linkedin","family":"social_media","billing_model":"auction","inventory_kind":"benchmark","source_snapshot_date":"2026-04-07"}'::jsonb::text,
        '{"buying_behaviour_fit":"quality_focused,brand_conscious","price_positioning_fit":"mid_range,premium,luxury","sales_model_fit":"direct_sales,hybrid","objective_fit_primary":"lead_generation","objective_fit_secondary":"awareness","environment_type":"professional_feed","premium_mass_fit":"premium","data_confidence":"medium","intelligence_notes":"Best used for B2B lead generation, higher-value professional audiences, and considered decision cycles."}'::jsonb,
        now(),
        now()
    )
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    media_type = EXCLUDED.media_type,
    coverage_type = EXCLUDED.coverage_type,
    catalog_health = EXCLUDED.catalog_health,
    operator_name = EXCLUDED.operator_name,
    is_national = EXCLUDED.is_national,
    has_pricing = EXCLUDED.has_pricing,
    language_notes = EXCLUDED.language_notes,
    audience_age_skew = EXCLUDED.audience_age_skew,
    audience_gender_skew = EXCLUDED.audience_gender_skew,
    audience_lsm_range = EXCLUDED.audience_lsm_range,
    target_audience = EXCLUDED.target_audience,
    data_source_enrichment = EXCLUDED.data_source_enrichment,
    strategy_fit_json = EXCLUDED.strategy_fit_json,
    updated_at = now();

INSERT INTO media_outlet_language (id, media_outlet_id, language_code, is_primary)
SELECT gen_random_uuid(), id, 'english', true
FROM media_outlet
WHERE code IN ('social_meta', 'social_tiktok', 'social_youtube', 'social_linkedin')
ON CONFLICT (media_outlet_id, language_code) DO NOTHING;

INSERT INTO media_outlet_keyword (id, media_outlet_id, keyword)
SELECT gen_random_uuid(), outlet.id, keyword.keyword
FROM (
    VALUES
        ('social_meta', 'facebook'),
        ('social_meta', 'instagram'),
        ('social_meta', 'reels'),
        ('social_meta', 'stories'),
        ('social_meta', 'retargeting'),
        ('social_tiktok', 'tiktok'),
        ('social_tiktok', 'short-form video'),
        ('social_tiktok', 'viral'),
        ('social_tiktok', 'discovery'),
        ('social_tiktok', 'video views'),
        ('social_youtube', 'youtube'),
        ('social_youtube', 'video'),
        ('social_youtube', 'pre-roll'),
        ('social_youtube', 'brand awareness'),
        ('social_linkedin', 'linkedin'),
        ('social_linkedin', 'b2b'),
        ('social_linkedin', 'professional'),
        ('social_linkedin', 'lead generation')
) AS keyword(code, keyword)
JOIN media_outlet outlet ON outlet.code = keyword.code
ON CONFLICT (media_outlet_id, keyword) DO NOTHING;

DELETE FROM media_outlet_pricing_package
WHERE media_outlet_id IN (
    SELECT id
    FROM media_outlet
    WHERE code IN ('social_meta', 'social_tiktok', 'social_youtube', 'social_linkedin')
);

INSERT INTO media_outlet_pricing_package (
    id, media_outlet_id, package_name, package_type, exposure_count, monthly_exposure_count,
    value_zar, investment_zar, cost_per_month_zar, duration_months, notes, source_name, source_date, is_active, created_at, updated_at
)
VALUES
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_meta'), 'Awareness benchmark - starter', 'social_benchmark', NULL, NULL, 9500, 7575, 7575, 1, '{"billing_model":"auction","billing_event":"cpm","benchmark_cpm_min_zar":17.00,"benchmark_cpm_max_zar":67.33,"benchmark_cpc_min_zar":4.38,"benchmark_cpc_max_zar":8.42,"recommended_daily_budget_zar":252.50}'::jsonb::text, 'Meta pricing + WebFX social benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_meta'), 'Awareness benchmark - scale', 'social_benchmark', NULL, NULL, 18000, 15000, 15000, 1, '{"billing_model":"auction","billing_event":"cpm","benchmark_cpm_min_zar":17.00,"benchmark_cpm_max_zar":67.33,"benchmark_cpc_min_zar":4.38,"benchmark_cpc_max_zar":8.42,"recommended_daily_budget_zar":500.00}'::jsonb::text, 'Meta pricing + WebFX social benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_tiktok'), 'Video views benchmark - starter', 'social_benchmark', NULL, NULL, 17500, 15150, 15150, 1, '{"billing_model":"auction","billing_event":"cpv","minimum_daily_budget_zar":336.66,"minimum_campaign_budget_zar":841.66,"benchmark_cpm_min_zar":54.03,"benchmark_cpm_max_zar":168.33,"benchmark_cpc_min_zar":4.21,"benchmark_cpc_max_zar":67.33,"benchmark_cpv_min_zar":0.17,"benchmark_cpv_max_zar":5.05}'::jsonb::text, 'TikTok official budget help + WebFX TikTok benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_tiktok'), 'Video views benchmark - scale', 'social_benchmark', NULL, NULL, 32000, 25000, 25000, 1, '{"billing_model":"auction","billing_event":"cpv","minimum_daily_budget_zar":336.66,"minimum_campaign_budget_zar":841.66,"benchmark_cpm_min_zar":54.03,"benchmark_cpm_max_zar":168.33,"benchmark_cpc_min_zar":4.21,"benchmark_cpc_max_zar":67.33,"benchmark_cpv_min_zar":0.17,"benchmark_cpv_max_zar":5.05}'::jsonb::text, 'TikTok official budget help + WebFX TikTok benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_youtube'), 'Video awareness benchmark - starter', 'social_benchmark', NULL, NULL, 12000, 10099, 10099, 1, '{"billing_model":"auction","billing_event":"cpv","benchmark_cpm_zar":162.94,"benchmark_cpc_min_zar":1.85,"benchmark_cpc_max_zar":6.73,"benchmark_cpv_min_zar":5.22,"benchmark_cpv_max_zar":6.73,"recommended_daily_budget_zar":336.66}'::jsonb::text, 'Google Ads docs + WebFX YouTube benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_youtube'), 'Video awareness benchmark - scale', 'social_benchmark', NULL, NULL, 22000, 18000, 18000, 1, '{"billing_model":"auction","billing_event":"cpv","benchmark_cpm_zar":162.94,"benchmark_cpc_min_zar":1.85,"benchmark_cpc_max_zar":6.73,"benchmark_cpv_min_zar":5.22,"benchmark_cpv_max_zar":6.73,"recommended_daily_budget_zar":600.00}'::jsonb::text, 'Google Ads docs + WebFX YouTube benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_linkedin'), 'Lead generation benchmark - starter', 'social_benchmark', NULL, NULL, 14500, 12625, 12625, 1, '{"billing_model":"auction","billing_event":"cpc","minimum_daily_budget_zar":168.33,"minimum_campaign_budget_zar":1683.31,"benchmark_cpm_min_zar":84.34,"benchmark_cpm_max_zar":134.66,"benchmark_cpc_min_zar":33.67,"benchmark_cpc_max_zar":50.50,"recommended_daily_budget_zar":420.83}'::jsonb::text, 'LinkedIn official pricing + budget help + WebFX LinkedIn benchmark', DATE '2026-04-07', true, now(), now()),
    (gen_random_uuid(), (SELECT id FROM media_outlet WHERE code = 'social_linkedin'), 'Lead generation benchmark - scale', 'social_benchmark', NULL, NULL, 28000, 22000, 22000, 1, '{"billing_model":"auction","billing_event":"cpc","minimum_daily_budget_zar":168.33,"minimum_campaign_budget_zar":1683.31,"benchmark_cpm_min_zar":84.34,"benchmark_cpm_max_zar":134.66,"benchmark_cpc_min_zar":33.67,"benchmark_cpc_max_zar":50.50,"recommended_daily_budget_zar":750.00}'::jsonb::text, 'LinkedIn official pricing + budget help + WebFX LinkedIn benchmark', DATE '2026-04-07', true, now(), now());

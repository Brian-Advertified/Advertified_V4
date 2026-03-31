CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS package_bands (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    min_budget NUMERIC(12,2) NOT NULL,
    max_budget NUMERIC(12,2) NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO package_bands (code, name, min_budget, max_budget, sort_order, is_active)
VALUES
    ('launch', 'Launch', 20000.00, 100000.00, 1, TRUE),
    ('boost', 'Boost', 100000.00, 500000.00, 2, TRUE),
    ('scale', 'Scale', 500000.00, 1000000.00, 3, TRUE),
    ('dominance', 'Dominance', 1000000.00, 5000000.00, 4, TRUE)
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    min_budget = EXCLUDED.min_budget,
    max_budget = EXCLUDED.max_budget,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active;

CREATE TABLE IF NOT EXISTS package_band_profiles (
    package_band_id UUID PRIMARY KEY REFERENCES package_bands(id) ON DELETE CASCADE,
    description TEXT NOT NULL,
    audience_fit TEXT NOT NULL,
    quick_benefit TEXT NOT NULL,
    package_purpose TEXT NOT NULL,
    include_radio VARCHAR(20) NOT NULL DEFAULT 'optional',
    include_tv VARCHAR(20) NOT NULL DEFAULT 'no',
    lead_time_label VARCHAR(100) NOT NULL,
    recommended_spend NUMERIC(12,2),
    is_recommended BOOLEAN NOT NULL DEFAULT FALSE,
    benefits_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE package_band_profiles ADD COLUMN IF NOT EXISTS include_radio VARCHAR(20) NOT NULL DEFAULT 'optional';
ALTER TABLE package_band_profiles ADD COLUMN IF NOT EXISTS include_tv VARCHAR(20) NOT NULL DEFAULT 'no';

CREATE TABLE IF NOT EXISTS package_band_preview_tiers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    package_band_id UUID NOT NULL REFERENCES package_bands(id) ON DELETE CASCADE,
    tier_code VARCHAR(20) NOT NULL,
    tier_label VARCHAR(120) NOT NULL,
    typical_inclusions_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    indicative_mix_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_package_band_preview_tiers_band_tier UNIQUE (package_band_id, tier_code)
);

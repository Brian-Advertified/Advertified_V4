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

CREATE TABLE IF NOT EXISTS admin_engine_policy_overrides (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    package_code VARCHAR(50) NOT NULL UNIQUE,
    budget_floor NUMERIC(12,2) NOT NULL,
    minimum_national_radio_candidates INTEGER NOT NULL,
    require_national_capable_radio BOOLEAN NOT NULL,
    require_premium_national_radio BOOLEAN NOT NULL,
    national_radio_bonus INTEGER NOT NULL,
    non_national_radio_penalty INTEGER NOT NULL,
    regional_radio_penalty INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

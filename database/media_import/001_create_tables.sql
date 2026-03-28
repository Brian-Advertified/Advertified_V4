CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================
-- STAGING TABLES
-- =========================

CREATE TABLE IF NOT EXISTS import_manifest (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    channel TEXT NOT NULL,
    page_count INTEGER,
    imported_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS raw_import_pages (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    channel TEXT,
    page INTEGER,
    page_text TEXT
);

CREATE TABLE IF NOT EXISTS package_document_metadata (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    channel TEXT,
    supplier_or_station TEXT,
    document_title TEXT,
    please_note TEXT
);

CREATE TABLE IF NOT EXISTS inventory_items (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    page INTEGER,
    site_title TEXT,
    site_description TEXT,
    city_province TEXT,
    media_format TEXT,
    site_number TEXT,
    rate_card_zar TEXT,
    discounted_rate_zar TEXT,
    city_town TEXT,
    suburb TEXT,
    address TEXT,
    production_flighting_zar TEXT,
    material TEXT,
    illuminated TEXT,
    lsm TEXT,
    available TEXT,
    traffic_count TEXT,
    gps_coordinates TEXT,
    dimensions TEXT,
    kmz_file TEXT
);

CREATE TABLE IF NOT EXISTS radio_packages (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    channel TEXT,
    supplier_or_station TEXT,
    element_name TEXT,
    exposure TEXT,
    value_zar TEXT,
    saving_or_discount_zar TEXT,
    investment_zar TEXT,
    duration TEXT,
    notes TEXT
);

CREATE TABLE IF NOT EXISTS radio_slot_grids (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    station TEXT NOT NULL,
    package_name TEXT,
    ad_length_seconds INTEGER,
    exposure_per_month_text TEXT,
    spots_count INTEGER,
    total_invoice_zar NUMERIC(12,2),
    package_cost_zar NUMERIC(12,2),
    avg_cost_per_spot_zar NUMERIC(12,2),
    monday_friday_windows TEXT,
    saturday_windows TEXT,
    sunday_windows TEXT,
    live_reads_allowed BOOLEAN,
    terms_excerpt TEXT,
    notes TEXT,
    raw_grid_excerpt TEXT
);

ALTER TABLE radio_slot_grids ADD COLUMN IF NOT EXISTS spots_count INTEGER;
ALTER TABLE radio_slot_grids ADD COLUMN IF NOT EXISTS total_invoice_zar NUMERIC(12,2);
ALTER TABLE radio_slot_grids ADD COLUMN IF NOT EXISTS package_cost_zar NUMERIC(12,2);
ALTER TABLE radio_slot_grids ADD COLUMN IF NOT EXISTS avg_cost_per_spot_zar NUMERIC(12,2);

CREATE TABLE IF NOT EXISTS sabc_rate_tables (
    id BIGSERIAL PRIMARY KEY,
    source_file TEXT NOT NULL,
    channel_type TEXT NOT NULL,
    product_name TEXT NOT NULL,
    package_cost_zar NUMERIC(12,2),
    spots_count INTEGER,
    avg_cost_per_spot_zar NUMERIC(12,2),
    exposure_value_zar NUMERIC(12,2),
    audience_segment TEXT,
    date_range_text TEXT,
    notes TEXT,
    raw_excerpt TEXT
);

-- =========================
-- FINAL TABLES
-- =========================

CREATE TABLE IF NOT EXISTS radio_stations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    normalized_name TEXT NOT NULL UNIQUE,
    station_group VARCHAR(100),
    market_scope VARCHAR(50),
    market_tier VARCHAR(50),
    monthly_listenership INTEGER,
    weekly_listenership INTEGER,
    audience_summary TEXT,
    primary_audience VARCHAR(100),
    secondary_audiences_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    language_summary VARCHAR(200),
    age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    lsm_min INTEGER,
    lsm_max INTEGER,
    coverage_summary TEXT,
    province_coverage_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    city_coverage_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    is_flagship_station BOOLEAN NOT NULL DEFAULT FALSE,
    is_premium_station BOOLEAN NOT NULL DEFAULT FALSE,
    brand_strength_score NUMERIC(5,2),
    coverage_score NUMERIC(5,2),
    audience_power_score NUMERIC(5,2),
    source_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS radio_packages_final (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    station_id UUID NOT NULL REFERENCES radio_stations(id) ON DELETE RESTRICT,
    name TEXT NOT NULL,
    total_cost NUMERIC(12,2),
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS radio_slots_final (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    station_id UUID NOT NULL REFERENCES radio_stations(id) ON DELETE RESTRICT,
    source_kind TEXT NOT NULL,
    time_band TEXT,
    day_type TEXT,
    slot_type TEXT,
    duration_seconds INTEGER,
    rate NUMERIC(12,2),
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS inventory_items_final (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    supplier TEXT,
    media_type TEXT,
    site_name TEXT,
    city TEXT,
    suburb TEXT,
    province TEXT,
    address TEXT,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_radio_packages_final_station_id ON radio_packages_final(station_id);
CREATE INDEX IF NOT EXISTS ix_radio_slots_final_station_id ON radio_slots_final(station_id);
CREATE INDEX IF NOT EXISTS ix_inventory_items_final_media_type ON inventory_items_final(media_type);

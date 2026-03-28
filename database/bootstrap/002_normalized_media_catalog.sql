CREATE TABLE IF NOT EXISTS source_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_file_name TEXT NOT NULL,
    source_path TEXT,
    media_channel VARCHAR(50) NOT NULL,
    supplier_name TEXT,
    document_title TEXT,
    document_type VARCHAR(50),
    validity_start DATE,
    validity_end DATE,
    checksum_sha256 TEXT,
    extraction_status VARCHAR(50) NOT NULL DEFAULT 'pending',
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_source_documents_source_path ON source_documents(source_path);

CREATE TABLE IF NOT EXISTS source_document_pages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_document_id UUID NOT NULL REFERENCES source_documents(id) ON DELETE CASCADE,
    page_number INTEGER NOT NULL,
    raw_text TEXT,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_source_document_pages_doc_page UNIQUE (source_document_id, page_number)
);

CREATE TABLE IF NOT EXISTS audience_taxonomy (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(80) NOT NULL UNIQUE,
    display_name VARCHAR(120) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS content_taxonomy (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(80) NOT NULL UNIQUE,
    display_name VARCHAR(120) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS region_clusters (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(120) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS region_cluster_mappings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id UUID NOT NULL REFERENCES region_clusters(id) ON DELETE CASCADE,
    province VARCHAR(100),
    city VARCHAR(100),
    station_or_channel_name VARCHAR(150),
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_region_cluster_mappings_lookup
ON region_cluster_mappings (
    cluster_id,
    coalesce(lower(province), ''),
    coalesce(lower(city), ''),
    coalesce(lower(station_or_channel_name), '')
);

CREATE TABLE IF NOT EXISTS media_reference_sources (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    media_channel VARCHAR(50) NOT NULL,
    station_or_channel_name VARCHAR(150),
    show_or_programme_name VARCHAR(200),
    source_url TEXT NOT NULL UNIQUE,
    source_title TEXT,
    source_type VARCHAR(50),
    geography_summary TEXT,
    audience_summary TEXT,
    age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    language_summary TEXT,
    market_summary TEXT,
    notes TEXT,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS radio_shows (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    station_id UUID NOT NULL REFERENCES radio_stations(id) ON DELETE CASCADE,
    source_document_id UUID REFERENCES source_documents(id) ON DELETE SET NULL,
    show_name VARCHAR(200) NOT NULL,
    presenter_name VARCHAR(200),
    default_daypart VARCHAR(50),
    default_start_time TIME,
    default_end_time TIME,
    language VARCHAR(50),
    region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL,
    geography_scope VARCHAR(80),
    province VARCHAR(100),
    city VARCHAR(100),
    audience_summary TEXT,
    age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    source_url TEXT,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS audience_summary TEXT;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS source_url TEXT;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS show_importance VARCHAR(50);
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS audience_primary VARCHAR(100);
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS audience_secondary_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS content_genre VARCHAR(100);
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS show_listenership INTEGER;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS show_reach_score NUMERIC(5,2);
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS is_flagship_daypart BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE radio_shows ADD COLUMN IF NOT EXISTS premium_fit_score NUMERIC(5,2);

CREATE UNIQUE INDEX IF NOT EXISTS uq_radio_shows_station_name ON radio_shows(station_id, show_name);

CREATE TABLE IF NOT EXISTS radio_inventory_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    station_id UUID NOT NULL REFERENCES radio_stations(id) ON DELETE CASCADE,
    show_id UUID REFERENCES radio_shows(id) ON DELETE SET NULL,
    source_document_id UUID REFERENCES source_documents(id) ON DELETE SET NULL,
    inventory_kind VARCHAR(50) NOT NULL,
    inventory_name VARCHAR(255) NOT NULL,
    daypart VARCHAR(50),
    slot_type VARCHAR(50),
    duration_seconds INTEGER,
    rate_zar NUMERIC(12,2),
    package_cost_zar NUMERIC(12,2),
    language VARCHAR(50),
    lsm_min INTEGER,
    lsm_max INTEGER,
    region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL,
    geography_scope VARCHAR(80),
    province VARCHAR(100),
    city VARCHAR(100),
    audience_summary TEXT,
    age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    source_url TEXT,
    is_available BOOLEAN NOT NULL DEFAULT TRUE,
    valid_from DATE,
    valid_until DATE,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS audience_summary TEXT;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS source_url TEXT;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS inventory_tier VARCHAR(50);
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS estimated_reach INTEGER;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS preview_priority NUMERIC(5,2);
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS planning_priority NUMERIC(5,2);
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS is_entry_friendly BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS is_boost_friendly BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS is_scale_friendly BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE radio_inventory_items ADD COLUMN IF NOT EXISTS is_dominance_friendly BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS radio_inventory_audience_tags (
    radio_inventory_item_id UUID NOT NULL REFERENCES radio_inventory_items(id) ON DELETE CASCADE,
    audience_taxonomy_id UUID NOT NULL REFERENCES audience_taxonomy(id) ON DELETE CASCADE,
    PRIMARY KEY (radio_inventory_item_id, audience_taxonomy_id)
);

CREATE TABLE IF NOT EXISTS radio_inventory_content_tags (
    radio_inventory_item_id UUID NOT NULL REFERENCES radio_inventory_items(id) ON DELETE CASCADE,
    content_taxonomy_id UUID NOT NULL REFERENCES content_taxonomy(id) ON DELETE CASCADE,
    PRIMARY KEY (radio_inventory_item_id, content_taxonomy_id)
);

CREATE TABLE IF NOT EXISTS tv_channels (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    channel_name VARCHAR(150) NOT NULL UNIQUE,
    language VARCHAR(50),
    region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL,
    geography_scope VARCHAR(80),
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE tv_channels ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;

CREATE TABLE IF NOT EXISTS tv_programmes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    channel_id UUID NOT NULL REFERENCES tv_channels(id) ON DELETE CASCADE,
    source_document_id UUID REFERENCES source_documents(id) ON DELETE SET NULL,
    programme_name VARCHAR(200) NOT NULL,
    genre VARCHAR(100),
    daypart VARCHAR(50),
    start_time TIME,
    end_time TIME,
    language VARCHAR(50),
    region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL,
    audience_summary TEXT,
    age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    source_url TEXT,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE tv_programmes ADD COLUMN IF NOT EXISTS audience_summary TEXT;
ALTER TABLE tv_programmes ADD COLUMN IF NOT EXISTS age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE tv_programmes ADD COLUMN IF NOT EXISTS source_url TEXT;
ALTER TABLE tv_programmes ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_tv_programmes_channel_name ON tv_programmes(channel_id, programme_name);

CREATE TABLE IF NOT EXISTS tv_inventory_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    channel_id UUID NOT NULL REFERENCES tv_channels(id) ON DELETE CASCADE,
    programme_id UUID REFERENCES tv_programmes(id) ON DELETE SET NULL,
    source_document_id UUID REFERENCES source_documents(id) ON DELETE SET NULL,
    inventory_kind VARCHAR(50) NOT NULL,
    inventory_name VARCHAR(255) NOT NULL,
    slot_type VARCHAR(50),
    duration_seconds INTEGER,
    rate_zar NUMERIC(12,2),
    package_cost_zar NUMERIC(12,2),
    language VARCHAR(50),
    lsm_min INTEGER,
    lsm_max INTEGER,
    region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL,
    geography_scope VARCHAR(80),
    audience_summary TEXT,
    age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    source_url TEXT,
    is_available BOOLEAN NOT NULL DEFAULT TRUE,
    valid_from DATE,
    valid_until DATE,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE tv_inventory_items ADD COLUMN IF NOT EXISTS audience_summary TEXT;
ALTER TABLE tv_inventory_items ADD COLUMN IF NOT EXISTS age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE tv_inventory_items ADD COLUMN IF NOT EXISTS source_url TEXT;
ALTER TABLE tv_inventory_items ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;

ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS station_group VARCHAR(100);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS market_scope VARCHAR(50);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS market_tier VARCHAR(50);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS monthly_listenership INTEGER;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS weekly_listenership INTEGER;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS audience_summary TEXT;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS primary_audience VARCHAR(100);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS secondary_audiences_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS language_summary VARCHAR(200);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS age_groups_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS lsm_min INTEGER;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS lsm_max INTEGER;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS coverage_summary TEXT;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS province_coverage_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS city_coverage_json JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS is_flagship_station BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS is_premium_station BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS brand_strength_score NUMERIC(5,2);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS coverage_score NUMERIC(5,2);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS audience_power_score NUMERIC(5,2);
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS source_url TEXT;
ALTER TABLE radio_stations ADD COLUMN IF NOT EXISTS source_document_id UUID REFERENCES source_documents(id) ON DELETE SET NULL;

ALTER TABLE inventory_items_final ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;

CREATE TABLE IF NOT EXISTS tv_inventory_audience_tags (
    tv_inventory_item_id UUID NOT NULL REFERENCES tv_inventory_items(id) ON DELETE CASCADE,
    audience_taxonomy_id UUID NOT NULL REFERENCES audience_taxonomy(id) ON DELETE CASCADE,
    PRIMARY KEY (tv_inventory_item_id, audience_taxonomy_id)
);

CREATE TABLE IF NOT EXISTS tv_inventory_content_tags (
    tv_inventory_item_id UUID NOT NULL REFERENCES tv_inventory_items(id) ON DELETE CASCADE,
    content_taxonomy_id UUID NOT NULL REFERENCES content_taxonomy(id) ON DELETE CASCADE,
    PRIMARY KEY (tv_inventory_item_id, content_taxonomy_id)
);

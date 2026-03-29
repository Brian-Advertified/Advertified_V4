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

-- Legacy V1 broadcast structures were intentionally removed after the V2
-- media_outlet migration. Shared source-document and geography tables above
-- remain part of the supported schema.

ALTER TABLE inventory_items_final ADD COLUMN IF NOT EXISTS region_cluster_id UUID REFERENCES region_clusters(id) ON DELETE SET NULL;

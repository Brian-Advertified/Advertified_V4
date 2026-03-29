CREATE TABLE IF NOT EXISTS package_area_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_code VARCHAR(50) NOT NULL UNIQUE REFERENCES region_clusters(code) ON DELETE CASCADE,
    display_name VARCHAR(120) NOT NULL,
    description TEXT,
    fallback_locations_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    sort_order INTEGER NOT NULL DEFAULT 100,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO package_area_profiles (cluster_code, display_name, description, fallback_locations_json, sort_order, is_active)
VALUES
(
    'gauteng',
    'Gauteng',
    'Johannesburg, Pretoria, and surrounding commuter corridors.',
    '["Sandton, Johannesburg (premium commuter routes)","Sunnyside, Pretoria (high foot traffic)","Randburg, Johannesburg (urban visibility)"]'::jsonb,
    1,
    true
),
(
    'kzn',
    'KwaZulu-Natal',
    'Durban, Pietermaritzburg, and surrounding coastal commuter markets.',
    '["Durban CBD (strong commuter movement)","Umhlanga retail nodes (premium shopper visibility)","Pinetown corridors (commuter and local trade traffic)"]'::jsonb,
    2,
    true
),
(
    'western-cape',
    'Western Cape',
    'Cape Town and surrounding retail, lifestyle, and tourism markets.',
    '["Cape Town CBD (strong commuter visibility)","Century City, Cape Town (retail and commuter traffic)","Canal Walk area (high shopper movement)"]'::jsonb,
    3,
    true
),
(
    'eastern-cape',
    'Eastern Cape',
    'Gqeberha, East London, and regional coastal commuter markets.',
    '["Gqeberha CBD (regional visibility)","Walmer, Gqeberha (commuter movement)","East London CBD (urban retail traffic)"]'::jsonb,
    4,
    true
),
(
    'national',
    'National',
    'Multi-province or nationwide package coverage.',
    '["Top commuter corridors","Retail-led urban nodes","High-traffic regional routes"]'::jsonb,
    99,
    true
)
ON CONFLICT (cluster_code) DO UPDATE
SET display_name = excluded.display_name,
    description = excluded.description,
    fallback_locations_json = excluded.fallback_locations_json,
    sort_order = excluded.sort_order,
    is_active = excluded.is_active,
    updated_at = NOW();

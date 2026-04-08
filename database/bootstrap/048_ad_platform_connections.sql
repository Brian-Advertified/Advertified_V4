CREATE TABLE IF NOT EXISTS ad_platform_connections (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_user_id UUID NULL REFERENCES user_accounts(id) ON DELETE SET NULL,
    provider VARCHAR(40) NOT NULL,
    external_account_id VARCHAR(160) NOT NULL,
    account_name VARCHAR(200) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'active',
    access_token TEXT NULL,
    refresh_token TEXT NULL,
    token_expires_at TIMESTAMPTZ NULL,
    metadata_json JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_synced_at TIMESTAMPTZ NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_ad_platform_connections_provider_external_account
    ON ad_platform_connections(provider, external_account_id);

CREATE INDEX IF NOT EXISTS ix_ad_platform_connections_owner_user_id
    ON ad_platform_connections(owner_user_id);

CREATE TABLE IF NOT EXISTS campaign_ad_platform_links (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    ad_platform_connection_id UUID NOT NULL REFERENCES ad_platform_connections(id) ON DELETE CASCADE,
    external_campaign_id VARCHAR(160) NULL,
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(30) NOT NULL DEFAULT 'active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_campaign_ad_platform_links_campaign_connection
    ON campaign_ad_platform_links(campaign_id, ad_platform_connection_id);

CREATE INDEX IF NOT EXISTS ix_campaign_ad_platform_links_campaign_id
    ON campaign_ad_platform_links(campaign_id);

CREATE INDEX IF NOT EXISTS ix_campaign_ad_platform_links_connection_id
    ON campaign_ad_platform_links(ad_platform_connection_id);


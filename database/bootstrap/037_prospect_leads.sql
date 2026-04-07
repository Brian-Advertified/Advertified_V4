CREATE TABLE IF NOT EXISTS prospect_leads
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    full_name character varying(200) NOT NULL,
    email character varying(255) NOT NULL,
    phone character varying(30),
    source character varying(50) NOT NULL DEFAULT 'unknown',
    claimed_user_id uuid NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT prospect_leads_claimed_user_id_fkey
        FOREIGN KEY (claimed_user_id)
        REFERENCES user_accounts (id)
        ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_prospect_leads_email
    ON prospect_leads (email);

CREATE INDEX IF NOT EXISTS ix_prospect_leads_claimed_user_id
    ON prospect_leads (claimed_user_id);

ALTER TABLE campaigns
    ADD COLUMN IF NOT EXISTS prospect_lead_id uuid NULL;

ALTER TABLE package_orders
    ADD COLUMN IF NOT EXISTS prospect_lead_id uuid NULL;

ALTER TABLE campaigns
    ALTER COLUMN user_id DROP NOT NULL;

ALTER TABLE package_orders
    ALTER COLUMN user_id DROP NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'campaigns_prospect_lead_id_fkey'
    ) THEN
        ALTER TABLE campaigns
            ADD CONSTRAINT campaigns_prospect_lead_id_fkey
            FOREIGN KEY (prospect_lead_id)
            REFERENCES prospect_leads (id)
            ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'package_orders_prospect_lead_id_fkey'
    ) THEN
        ALTER TABLE package_orders
            ADD CONSTRAINT package_orders_prospect_lead_id_fkey
            FOREIGN KEY (prospect_lead_id)
            REFERENCES prospect_leads (id)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_campaigns_prospect_lead_id
    ON campaigns (prospect_lead_id);

CREATE INDEX IF NOT EXISTS ix_package_orders_prospect_lead_id
    ON package_orders (prospect_lead_id);

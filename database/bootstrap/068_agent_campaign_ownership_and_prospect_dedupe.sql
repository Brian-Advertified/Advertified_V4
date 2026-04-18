ALTER TABLE prospect_leads
    ADD COLUMN IF NOT EXISTS normalized_email character varying(255),
    ADD COLUMN IF NOT EXISTS normalized_phone character varying(30),
    ADD COLUMN IF NOT EXISTS owner_agent_user_id uuid NULL;

UPDATE prospect_leads
SET normalized_email = lower(trim(email))
WHERE normalized_email IS NULL
  AND email IS NOT NULL;

UPDATE prospect_leads
SET normalized_phone = CASE
    WHEN phone IS NULL OR btrim(phone) = '' THEN NULL
    ELSE
        CASE
            WHEN regexp_replace(phone, '[^0-9]', '', 'g') ~ '^27[0-9]{9}$'
                THEN '+' || regexp_replace(phone, '[^0-9]', '', 'g')
            WHEN regexp_replace(phone, '[^0-9]', '', 'g') ~ '^0[0-9]{9}$'
                THEN '+27' || substring(regexp_replace(phone, '[^0-9]', '', 'g') from 2)
            WHEN regexp_replace(phone, '[^0-9]', '', 'g') ~ '^[0-9]{9}$'
                THEN '+27' || regexp_replace(phone, '[^0-9]', '', 'g')
            ELSE regexp_replace(phone, '\s+', '', 'g')
        END
    END
WHERE normalized_phone IS NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'prospect_leads_owner_agent_user_id_fkey'
    ) THEN
        ALTER TABLE prospect_leads
            ADD CONSTRAINT prospect_leads_owner_agent_user_id_fkey
            FOREIGN KEY (owner_agent_user_id)
            REFERENCES user_accounts (id)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_prospect_leads_normalized_email
    ON prospect_leads (normalized_email);

CREATE INDEX IF NOT EXISTS ix_prospect_leads_normalized_phone
    ON prospect_leads (normalized_phone);

CREATE INDEX IF NOT EXISTS ix_prospect_leads_owner_agent_user_id
    ON prospect_leads (owner_agent_user_id);

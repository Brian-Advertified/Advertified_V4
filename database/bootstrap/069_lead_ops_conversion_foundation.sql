ALTER TABLE prospect_leads
    ADD COLUMN IF NOT EXISTS source_lead_id integer NULL,
    ADD COLUMN IF NOT EXISTS last_contacted_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS next_follow_up_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS sla_due_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS last_outcome text NULL;

CREATE INDEX IF NOT EXISTS ix_prospect_leads_source_lead_id
    ON prospect_leads (source_lead_id);

CREATE INDEX IF NOT EXISTS ix_prospect_leads_next_follow_up_at
    ON prospect_leads (next_follow_up_at);

CREATE INDEX IF NOT EXISTS ix_prospect_leads_sla_due_at
    ON prospect_leads (sla_due_at);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'prospect_leads_source_lead_id_fkey'
    ) THEN
        ALTER TABLE prospect_leads
            ADD CONSTRAINT prospect_leads_source_lead_id_fkey
            FOREIGN KEY (source_lead_id)
            REFERENCES leads (id)
            ON DELETE SET NULL;
    END IF;
END $$;

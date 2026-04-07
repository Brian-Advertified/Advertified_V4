ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS source varchar(100) NOT NULL DEFAULT 'manual',
    ADD COLUMN IF NOT EXISTS source_reference varchar(500),
    ADD COLUMN IF NOT EXISTS last_discovered_at timestamptz;

UPDATE leads
SET source = COALESCE(NULLIF(source, ''), 'manual')
WHERE source IS NULL OR source = '';

CREATE INDEX IF NOT EXISTS ix_leads_source
    ON leads (source);

CREATE INDEX IF NOT EXISTS ix_leads_source_reference
    ON leads (source_reference);

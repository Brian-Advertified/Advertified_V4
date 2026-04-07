ALTER TABLE lead_actions
    ADD COLUMN IF NOT EXISTS assigned_agent_user_id uuid NULL,
    ADD COLUMN IF NOT EXISTS assigned_at timestamptz NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'lead_actions_assigned_agent_user_id_fkey'
    ) THEN
        ALTER TABLE lead_actions
            ADD CONSTRAINT lead_actions_assigned_agent_user_id_fkey
            FOREIGN KEY (assigned_agent_user_id)
            REFERENCES user_accounts (id)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_lead_actions_assigned_agent_user_id
    ON lead_actions (assigned_agent_user_id);

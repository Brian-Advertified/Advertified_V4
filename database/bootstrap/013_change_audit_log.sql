CREATE TABLE IF NOT EXISTS change_audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id UUID NULL REFERENCES user_accounts(id) ON DELETE SET NULL,
    actor_role VARCHAR(50) NOT NULL DEFAULT '',
    actor_name VARCHAR(200) NOT NULL DEFAULT '',
    actor_email VARCHAR(255) NOT NULL DEFAULT '',
    scope VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id VARCHAR(200) NOT NULL,
    entity_label VARCHAR(255),
    summary TEXT NOT NULL,
    metadata_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_change_audit_log_created_at ON change_audit_log(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_change_audit_log_scope ON change_audit_log(scope);
CREATE INDEX IF NOT EXISTS ix_change_audit_log_actor_user_id ON change_audit_log(actor_user_id);

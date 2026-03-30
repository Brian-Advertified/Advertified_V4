CREATE TABLE IF NOT EXISTS notification_read_receipts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES user_accounts(id) ON DELETE CASCADE,
    notification_id VARCHAR(200) NOT NULL,
    read_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_notification_read_receipts_user_notification
    ON notification_read_receipts(user_id, notification_id);

CREATE INDEX IF NOT EXISTS ix_notification_read_receipts_user_id
    ON notification_read_receipts(user_id);

ALTER TABLE email_delivery_messages
    ADD COLUMN IF NOT EXISTS body_html text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS attachments_json jsonb NULL;

UPDATE email_delivery_messages
SET body_html = ''
WHERE body_html IS NULL;

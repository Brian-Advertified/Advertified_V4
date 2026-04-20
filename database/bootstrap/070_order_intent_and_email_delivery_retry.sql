ALTER TABLE package_orders
    ADD COLUMN IF NOT EXISTS order_intent varchar(20) NOT NULL DEFAULT 'sale';

UPDATE package_orders
SET order_intent = 'prospect'
WHERE (order_intent IS NULL OR btrim(order_intent) = '')
  AND lower(coalesce(payment_provider, '')) = 'prospect';

UPDATE package_orders
SET order_intent = 'sale'
WHERE order_intent IS NULL OR btrim(order_intent) = '';

CREATE INDEX IF NOT EXISTS ix_package_orders_order_intent
    ON package_orders (order_intent);

CREATE INDEX IF NOT EXISTS ix_package_orders_order_intent_payment_status
    ON package_orders (order_intent, payment_status);

ALTER TABLE email_delivery_messages
    ADD COLUMN IF NOT EXISTS attempt_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_attempt_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS locked_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS lock_token uuid NULL;

CREATE INDEX IF NOT EXISTS ix_email_delivery_messages_next_attempt_at
    ON email_delivery_messages (next_attempt_at);

CREATE INDEX IF NOT EXISTS ix_email_delivery_messages_status_next_attempt_at
    ON email_delivery_messages (status, next_attempt_at);

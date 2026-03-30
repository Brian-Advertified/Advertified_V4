alter table package_orders
    add column if not exists refund_status varchar(50) not null default 'none',
    add column if not exists refunded_amount numeric(12,2) not null default 0,
    add column if not exists gateway_fee_retained_amount numeric(12,2) not null default 0,
    add column if not exists refund_reason text null,
    add column if not exists refund_processed_at timestamp without time zone null;

alter table campaigns
    add column if not exists paused_at timestamp without time zone null,
    add column if not exists total_paused_days integer not null default 0,
    add column if not exists pause_reason text null;

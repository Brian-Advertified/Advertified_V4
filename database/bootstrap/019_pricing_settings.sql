create table if not exists pricing_settings (
    pricing_key varchar(50) primary key,
    ai_studio_reserve_percent numeric(8,4) not null,
    ooh_markup_percent numeric(8,4) not null,
    radio_markup_percent numeric(8,4) not null,
    tv_markup_percent numeric(8,4) not null,
    newspaper_markup_percent numeric(8,4) not null default 0.1500,
    digital_markup_percent numeric(8,4) not null default 0.1500,
    sales_commission_percent numeric(8,4) not null default 0.1000,
    sales_commission_threshold_zar numeric(12,2) not null default 250000.00,
    sales_agent_share_below_threshold_percent numeric(8,4) not null default 0.6000,
    sales_agent_share_at_or_above_threshold_percent numeric(8,4) not null default 0.5000,
    created_at timestamp with time zone not null default now(),
    updated_at timestamp with time zone not null default now()
);

alter table pricing_settings add column if not exists digital_markup_percent numeric(8,4) not null default 0.1500;
alter table pricing_settings add column if not exists newspaper_markup_percent numeric(8,4) not null default 0.1500;
alter table pricing_settings add column if not exists sales_commission_percent numeric(8,4) not null default 0.1000;
alter table pricing_settings add column if not exists sales_commission_threshold_zar numeric(12,2) not null default 250000.00;
alter table pricing_settings add column if not exists sales_agent_share_below_threshold_percent numeric(8,4) not null default 0.6000;
alter table pricing_settings add column if not exists sales_agent_share_at_or_above_threshold_percent numeric(8,4) not null default 0.5000;

insert into pricing_settings (
    pricing_key,
    ai_studio_reserve_percent,
    ooh_markup_percent,
    radio_markup_percent,
    tv_markup_percent,
    newspaper_markup_percent,
    digital_markup_percent,
    sales_commission_percent,
    sales_commission_threshold_zar,
    sales_agent_share_below_threshold_percent,
    sales_agent_share_at_or_above_threshold_percent
)
values (
    'default',
    0.1000,
    0.1500,
    0.1500,
    0.1500,
    0.1500,
    0.1500,
    0.1000,
    250000.00,
    0.6000,
    0.5000
)
on conflict (pricing_key) do nothing;

alter table package_orders add column if not exists ai_studio_reserve_percent numeric(8,4) not null default 0.1000;
alter table package_orders add column if not exists ai_studio_reserve_amount numeric(12,2) not null default 0;
alter table package_orders add column if not exists sales_commission_percent numeric(8,4) not null default 0.1000;
alter table package_orders add column if not exists sales_commission_pool_amount numeric(12,2) not null default 0;
alter table package_orders add column if not exists sales_agent_commission_share_percent numeric(8,4) not null default 0;
alter table package_orders add column if not exists sales_agent_commission_amount numeric(12,2) not null default 0;
alter table package_orders add column if not exists advertified_sales_commission_amount numeric(12,2) not null default 0;
alter table package_orders add column if not exists sales_commission_tier varchar(50) not null default 'none';

alter table pricing_settings
    add column if not exists digital_markup_percent numeric(8,4) not null default 0.1000;

alter table pricing_settings
    add column if not exists newspaper_markup_percent numeric(8,4) not null default 0.1000;

alter table pricing_settings
    add column if not exists sales_commission_percent numeric(8,4) not null default 0.1000;

alter table pricing_settings
    add column if not exists sales_commission_threshold_zar numeric(12,2) not null default 250000.00;

alter table pricing_settings
    add column if not exists sales_agent_share_below_threshold_percent numeric(8,4) not null default 0.6000;

alter table pricing_settings
    add column if not exists sales_agent_share_at_or_above_threshold_percent numeric(8,4) not null default 0.5000;

update pricing_settings
set digital_markup_percent = 0.1500
where digital_markup_percent is null or digital_markup_percent = 0.1000;

update pricing_settings
set newspaper_markup_percent = 0.1500
where newspaper_markup_percent is null or newspaper_markup_percent = 0.1000;

update pricing_settings
set radio_markup_percent = 0.1500
where radio_markup_percent = 0.1000;

update pricing_settings
set tv_markup_percent = 0.1500
where tv_markup_percent = 0.1000;

update pricing_settings
set ooh_markup_percent = 0.1500
where ooh_markup_percent = 0.0500;

update pricing_settings
set sales_commission_percent = 0.1000
where sales_commission_percent is null;

update pricing_settings
set sales_commission_threshold_zar = 250000.00
where sales_commission_threshold_zar is null;

update pricing_settings
set sales_agent_share_below_threshold_percent = 0.6000
where sales_agent_share_below_threshold_percent is null;

update pricing_settings
set sales_agent_share_at_or_above_threshold_percent = 0.5000
where sales_agent_share_at_or_above_threshold_percent is null;

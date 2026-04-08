alter table pricing_settings
    add column if not exists digital_markup_percent numeric(8,4) not null default 0.1000;

update pricing_settings
set digital_markup_percent = 0.1000
where digital_markup_percent is null;

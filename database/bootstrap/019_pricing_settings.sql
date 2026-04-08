create table if not exists pricing_settings (
    pricing_key varchar(50) primary key,
    ai_studio_reserve_percent numeric(8,4) not null,
    ooh_markup_percent numeric(8,4) not null,
    radio_markup_percent numeric(8,4) not null,
    tv_markup_percent numeric(8,4) not null,
    digital_markup_percent numeric(8,4) not null default 0.1000,
    created_at timestamp with time zone not null default now(),
    updated_at timestamp with time zone not null default now()
);

alter table pricing_settings add column if not exists digital_markup_percent numeric(8,4) not null default 0.1000;

insert into pricing_settings (
    pricing_key,
    ai_studio_reserve_percent,
    ooh_markup_percent,
    radio_markup_percent,
    tv_markup_percent,
    digital_markup_percent
)
values (
    'default',
    0.1000,
    0.0500,
    0.1000,
    0.1000,
    0.1000
)
on conflict (pricing_key) do nothing;

alter table package_orders add column if not exists ai_studio_reserve_percent numeric(8,4) not null default 0.1000;
alter table package_orders add column if not exists ai_studio_reserve_amount numeric(12,2) not null default 0;

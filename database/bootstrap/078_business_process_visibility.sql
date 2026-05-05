alter table package_orders
    add column if not exists lost_reason text null,
    add column if not exists lost_stage varchar(50) null,
    add column if not exists lost_at timestamp without time zone null,
    add column if not exists terms_accepted_at timestamp without time zone null,
    add column if not exists terms_version varchar(50) null,
    add column if not exists terms_acceptance_source varchar(50) null,
    add column if not exists cancellation_status varchar(50) not null default 'none',
    add column if not exists cancellation_reason text null,
    add column if not exists cancellation_requested_at timestamp without time zone null;

alter table campaign_recommendations
    add column if not exists estimated_supplier_cost numeric(12,2) not null default 0,
    add column if not exists estimated_gross_profit numeric(12,2) not null default 0,
    add column if not exists estimated_gross_margin_percent numeric(8,4) null,
    add column if not exists margin_status varchar(50) not null default 'unchecked',
    add column if not exists client_explanation text null,
    add column if not exists supplier_availability_status varchar(50) not null default 'unconfirmed',
    add column if not exists supplier_availability_checked_at timestamp without time zone null,
    add column if not exists supplier_availability_notes text null;

alter table campaign_supplier_bookings
    add column if not exists availability_status varchar(50) not null default 'unconfirmed',
    add column if not exists availability_checked_at timestamp without time zone null,
    add column if not exists supplier_confirmation_reference varchar(120) null,
    add column if not exists confirmed_at timestamp without time zone null;

create index if not exists ix_package_orders_lost_stage on package_orders(lost_stage);
create index if not exists ix_package_orders_terms_accepted_at on package_orders(terms_accepted_at);
create index if not exists ix_campaign_recommendations_margin_status on campaign_recommendations(margin_status);
create index if not exists ix_campaign_recommendations_supplier_availability_status on campaign_recommendations(supplier_availability_status);
create index if not exists ix_campaign_supplier_bookings_availability_status on campaign_supplier_bookings(availability_status);

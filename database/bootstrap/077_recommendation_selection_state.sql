alter table if exists package_orders
    add column if not exists selected_recommendation_id uuid null;

alter table if exists package_orders
    add column if not exists selected_at timestamp without time zone null;

alter table if exists package_orders
    add column if not exists selection_source varchar(50) null;

alter table if exists package_orders
    add column if not exists selection_status varchar(50) not null default 'none';

create index if not exists ix_package_orders_selected_recommendation_id
    on package_orders (selected_recommendation_id);

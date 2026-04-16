alter table if exists radio_inventory_intelligence
    add column if not exists household_decision_maker_fit varchar(20) null;

alter table if exists radio_inventory_intelligence
    add column if not exists genre_fit varchar(80) null;

alter table if exists tv_inventory_intelligence
    add column if not exists household_decision_maker_fit varchar(20) null;

alter table if exists ooh_inventory_intelligence
    drop constraint if exists ooh_inventory_intelligence_source_inventory_id_fkey;

drop index if exists ix_ooh_inventory_intelligence_source_inventory_id;

alter table if exists ooh_inventory_intelligence
    drop column if exists source_inventory_id;

drop table if exists inventory_items_final cascade;
drop table if exists inventory_items cascade;
drop table if exists raw_import_pages cascade;

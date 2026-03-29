alter table if exists campaign_recommendations
    add column if not exists pdf_storage_object_key text null;

alter table if exists campaign_recommendations
    add column if not exists pdf_generated_at timestamp without time zone null;

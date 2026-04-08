alter table if exists campaign_recommendations
    add column if not exists inventory_batch_refs_json jsonb null;

alter table if exists recommendation_run_audits
    add column if not exists inventory_batch_refs_json jsonb null;

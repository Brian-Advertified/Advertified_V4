alter table if exists campaign_recommendations
    add column if not exists revision_number integer not null default 1;

alter table if exists campaign_recommendations
    add column if not exists sent_to_client_at timestamp without time zone null;

alter table if exists campaign_recommendations
    add column if not exists approved_at timestamp without time zone null;

create index if not exists ix_campaign_recommendations_campaign_revision
    on campaign_recommendations (campaign_id, revision_number desc, created_at desc);

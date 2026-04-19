alter table if exists campaign_briefs
    add column if not exists channel_flights_json jsonb;

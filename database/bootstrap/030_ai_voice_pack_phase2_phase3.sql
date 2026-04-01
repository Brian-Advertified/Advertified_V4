alter table if exists ai_voice_packs
    add column if not exists is_client_specific boolean not null default false,
    add column if not exists client_user_id uuid null,
    add column if not exists is_cloned_voice boolean not null default false,
    add column if not exists audience_tags_json jsonb not null default '[]'::jsonb,
    add column if not exists objective_tags_json jsonb not null default '[]'::jsonb;

create index if not exists ix_ai_voice_packs_client_user_id
    on ai_voice_packs (client_user_id);

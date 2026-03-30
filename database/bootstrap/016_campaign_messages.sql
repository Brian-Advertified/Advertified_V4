create table if not exists campaign_conversations (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    client_user_id uuid not null references user_accounts(id) on delete restrict,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    last_message_at timestamp without time zone null
);

create unique index if not exists uq_campaign_conversations_campaign_id
    on campaign_conversations (campaign_id);

create index if not exists ix_campaign_conversations_client_user_id
    on campaign_conversations (client_user_id);

create index if not exists ix_campaign_conversations_last_message_at
    on campaign_conversations (last_message_at desc nulls last);

create table if not exists campaign_messages (
    id uuid primary key default gen_random_uuid(),
    conversation_id uuid not null references campaign_conversations(id) on delete cascade,
    sender_user_id uuid not null references user_accounts(id) on delete restrict,
    sender_role varchar(20) not null,
    body text not null,
    created_at timestamp without time zone not null default now(),
    read_by_client_at timestamp without time zone null,
    read_by_agent_at timestamp without time zone null,
    email_notification_sent_at timestamp without time zone null
);

create index if not exists ix_campaign_messages_conversation_id
    on campaign_messages (conversation_id);

create index if not exists ix_campaign_messages_sender_user_id
    on campaign_messages (sender_user_id);

create index if not exists ix_campaign_messages_created_at
    on campaign_messages (created_at desc);

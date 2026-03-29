alter table campaigns
    add column if not exists assignment_email_sent_at timestamp without time zone null,
    add column if not exists agent_work_started_email_sent_at timestamp without time zone null,
    add column if not exists recommendation_ready_email_sent_at timestamp without time zone null;

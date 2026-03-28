create extension if not exists pgcrypto;

create table if not exists email_templates (
    id uuid primary key default gen_random_uuid(),
    template_name varchar(120) not null,
    subject_template text not null,
    body_html_template text not null,
    is_active boolean not null default true,
    created_at_utc timestamp without time zone not null default timezone('utc', now()),
    updated_at_utc timestamp without time zone not null default timezone('utc', now())
);

create unique index if not exists uq_email_templates_template_name
    on email_templates (template_name);

create index if not exists ix_email_templates_is_active
    on email_templates (is_active);

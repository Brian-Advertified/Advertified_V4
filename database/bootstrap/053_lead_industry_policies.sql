create table if not exists lead_industry_policies (
    key varchar(100) primary key,
    name varchar(200) not null,
    objective_override varchar(100) null,
    preferred_tone varchar(100) null,
    preferred_channels_json jsonb not null default '[]'::jsonb,
    cta text not null,
    messaging_angle text not null,
    guardrails_json jsonb not null default '[]'::jsonb,
    additional_gap text not null default '',
    additional_outcome text not null default '',
    sort_order integer not null default 0,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_lead_industry_policies_active_sort
    on lead_industry_policies (is_active, sort_order, key);

insert into lead_industry_policies (
    key,
    name,
    objective_override,
    preferred_tone,
    preferred_channels_json,
    cta,
    messaging_angle,
    guardrails_json,
    additional_gap,
    additional_outcome,
    sort_order,
    is_active
)
values
    (
        'funeral_services',
        'Funeral Services',
        'leads',
        'balanced',
        '["Search", "Radio", "OOH"]'::jsonb,
        'Speak to our team for guidance',
        'trust, dignity, and immediate local support',
        '["Avoid aggressive urgency or discount framing.", "Use compassionate, respectful language.", "Prioritize service trust over hard-sell tactics."]'::jsonb,
        'Opportunity to improve trust-led local discoverability for urgent family decisions.',
        'Expected impact: improved qualified enquiries and stronger community trust presence.',
        10,
        true
    ),
    (
        'healthcare',
        'Healthcare',
        'leads',
        'balanced',
        '["Search", "OOH", "Radio"]'::jsonb,
        'Book a consultation',
        'credibility, safety, and clear local access',
        '["Avoid absolute treatment claims.", "Keep language clear and compliant.", "Lead with trust and accessibility."]'::jsonb,
        'Opportunity to strengthen high-intent service capture for nearby patients.',
        'Expected impact: higher consultation intent and stronger local appointment flow.',
        20,
        true
    ),
    (
        'legal_services',
        'Legal Services',
        'leads',
        'performance',
        '["Search", "Radio", "Digital"]'::jsonb,
        'Request legal guidance',
        'authority, clarity, and response confidence',
        '["Avoid guaranteed case outcomes.", "Use precise, professional language.", "Focus on trust and next-step clarity."]'::jsonb,
        'Opportunity to capture urgent high-intent searches before competitor firms.',
        'Expected impact: improved lead quality and stronger inbound case enquiries.',
        30,
        true
    ),
    (
        'retail',
        'Retail',
        'promotion',
        'performance',
        '["OOH", "Radio", "Digital"]'::jsonb,
        'Visit us today',
        'local visibility and repeat customer flow',
        '["Balance promo with brand consistency.", "Avoid over-reliance on discount-only messaging.", "Keep campaign continuity between promo windows."]'::jsonb,
        'Opportunity to convert promotional momentum into always-on visibility.',
        'Expected impact: steadier footfall and stronger repeat demand beyond promotions.',
        40,
        true
    ),
    (
        'default',
        'General Services',
        null,
        'balanced',
        '["Digital", "OOH"]'::jsonb,
        'Contact us to get started',
        'visible, credible, and easy to act on',
        '["Keep claims realistic and evidence-based.", "Use clear, practical CTA language.", "Align messaging with local demand intent."]'::jsonb,
        'Opportunity to tighten channel mix around the strongest local demand signals.',
        'Expected impact: clearer positioning and better conversion from existing demand.',
        100,
        true
    )
on conflict (key) do nothing;

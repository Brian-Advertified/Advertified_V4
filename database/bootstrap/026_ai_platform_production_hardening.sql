create table if not exists ai_prompt_templates (
    id uuid primary key default gen_random_uuid(),
    key varchar(120) not null,
    channel varchar(40) not null default 'Digital',
    language varchar(40) not null default 'English',
    version integer not null,
    system_prompt text not null,
    template_prompt text not null,
    output_schema text not null,
    variables_json jsonb not null default '[]'::jsonb,
    is_active boolean not null default true,
    created_at timestamp without time zone not null default now()
);

alter table ai_prompt_templates add column if not exists channel varchar(40) not null default 'Digital';
alter table ai_prompt_templates add column if not exists language varchar(40) not null default 'English';
alter table ai_prompt_templates add column if not exists variables_json jsonb not null default '[]'::jsonb;
alter table ai_prompt_templates add column if not exists version_label varchar(30) not null default 'v1';
alter table ai_prompt_templates add column if not exists performance_score numeric(5,2) null;
alter table ai_prompt_templates add column if not exists usage_count integer not null default 0;
alter table ai_prompt_templates add column if not exists base_system_prompt_key varchar(120) null;

drop index if exists uq_ai_prompt_templates_key_version;
drop index if exists ix_ai_prompt_templates_key_active;
create unique index if not exists uq_ai_prompt_templates_key_channel_language_version
    on ai_prompt_templates(key, channel, language, version);
create index if not exists ix_ai_prompt_templates_key_channel_language_active
    on ai_prompt_templates(key, channel, language, is_active);

create table if not exists ai_creative_job_statuses (
    job_id uuid primary key,
    campaign_id uuid not null references campaigns(id) on delete cascade,
    status varchar(40) not null,
    error text null,
    retry_attempt_count integer not null default 0,
    last_failure text null,
    updated_at timestamp without time zone not null default now()
);

alter table ai_creative_job_statuses add column if not exists retry_attempt_count integer not null default 0;
alter table ai_creative_job_statuses add column if not exists last_failure text null;

create index if not exists ix_ai_creative_job_statuses_campaign_id
    on ai_creative_job_statuses(campaign_id);
create index if not exists ix_ai_creative_job_statuses_updated_at
    on ai_creative_job_statuses(updated_at desc);

create table if not exists ai_creative_qa_results (
    id uuid primary key default gen_random_uuid(),
    creative_id uuid not null,
    campaign_id uuid not null references campaigns(id) on delete cascade,
    channel varchar(40) not null,
    language varchar(40) not null default 'English',
    clarity numeric(5,2) not null default 0,
    attention numeric(5,2) not null default 0,
    emotional_impact numeric(5,2) not null default 0,
    cta_strength numeric(5,2) not null default 0,
    brand_fit numeric(5,2) not null default 0,
    channel_fit numeric(5,2) not null default 0,
    final_score numeric(5,2) not null default 0,
    status varchar(40) not null,
    risk_level varchar(20) not null default 'Low',
    issues_json jsonb not null default '[]'::jsonb,
    suggestions_json jsonb not null default '[]'::jsonb,
    risk_flags_json jsonb not null default '[]'::jsonb,
    improved_payload_json text null,
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_ai_creative_qa_results_campaign_id
    on ai_creative_qa_results(campaign_id);
create index if not exists ix_ai_creative_qa_results_creative_id
    on ai_creative_qa_results(creative_id);
create index if not exists ix_ai_creative_qa_results_created_at
    on ai_creative_qa_results(created_at desc);

create table if not exists ai_asset_jobs (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    creative_id uuid not null,
    asset_kind varchar(40) not null,
    provider varchar(80) not null,
    status varchar(20) not null default 'queued',
    request_json jsonb not null,
    result_json jsonb null,
    asset_url text null,
    asset_type varchar(30) null,
    error text null,
    retry_attempt_count integer not null default 0,
    last_failure text null,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    completed_at timestamp without time zone null
);

alter table ai_asset_jobs add column if not exists retry_attempt_count integer not null default 0;
alter table ai_asset_jobs add column if not exists last_failure text null;

create index if not exists ix_ai_asset_jobs_campaign_id on ai_asset_jobs(campaign_id);
create index if not exists ix_ai_asset_jobs_creative_id on ai_asset_jobs(creative_id);
create index if not exists ix_ai_asset_jobs_status on ai_asset_jobs(status);
create index if not exists ix_ai_asset_jobs_created_at on ai_asset_jobs(created_at desc);

create table if not exists ai_creative_job_dead_letters (
    id uuid primary key default gen_random_uuid(),
    job_id uuid not null,
    campaign_id uuid not null references campaigns(id) on delete cascade,
    reason text not null,
    created_at timestamp without time zone not null default now()
);

create index if not exists ix_ai_creative_job_dead_letters_job_id
    on ai_creative_job_dead_letters(job_id);
create index if not exists ix_ai_creative_job_dead_letters_created_at
    on ai_creative_job_dead_letters(created_at desc);

create table if not exists ai_idempotency_records (
    id uuid primary key default gen_random_uuid(),
    scope varchar(80) not null,
    key varchar(256) not null,
    job_id uuid not null,
    created_at timestamp without time zone not null default now()
);

create unique index if not exists uq_ai_idempotency_records_scope_key
    on ai_idempotency_records(scope, key);
create index if not exists ix_ai_idempotency_records_created_at
    on ai_idempotency_records(created_at desc);

create table if not exists ai_usage_logs (
    id uuid primary key default gen_random_uuid(),
    campaign_id uuid not null references campaigns(id) on delete cascade,
    creative_id uuid null,
    job_id uuid null,
    operation varchar(80) not null,
    provider varchar(80) null,
    estimated_cost_zar numeric(12,2) not null default 0,
    actual_cost_zar numeric(12,2) null,
    status varchar(20) not null default 'reserved',
    details text null,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now()
);

create index if not exists ix_ai_usage_logs_campaign_id
    on ai_usage_logs(campaign_id);
create index if not exists ix_ai_usage_logs_status
    on ai_usage_logs(status);
create index if not exists ix_ai_usage_logs_created_at
    on ai_usage_logs(created_at desc);

insert into ai_prompt_templates (key, channel, language, version, version_label, system_prompt, template_prompt, output_schema, variables_json, performance_score, usage_count, base_system_prompt_key, is_active)
values
('creative-global-system', 'Digital', 'English', 1, 'v1.0', 'You are a senior South African advertising creative director.\n\nYou create high-converting ads tailored to:\n- Local culture\n- Language nuances (English, Zulu, Xhosa, Afrikaans)\n- Audience LSM segments\n\nRules:\n- Be concise and impactful\n- Avoid generic phrases\n- Focus on ONE clear message\n- Always include a strong call to action\n- Adapt tone to audience (township vs suburban vs corporate)\n\nReturn ONLY valid JSON. No explanations.', 'Apply all rules to the generated creative output.', '{"type":"object"}', '[]', 9.00, 0, null, true),
('creative-brief-default', 'Digital', 'English', 1, 'v1.0', 'Return valid JSON creative output.', 'Generate channel-native variants aligned to objective, audience and CTA.', '{"type":"object","required":["channel","language","creative","cta"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"objective","Description":"Campaign objective","IsRequired":true},{"Name":"offer","Description":"Offer/message","IsRequired":false}]', 8.70, 0, 'creative-global-system', true),
('creative-brief-radio', 'Radio', 'English', 1, 'v1.0', 'Create a structured radio ad response.', 'Create a {{duration}}-second radio advertisement.\n\nInputs:\n- Brand: {{brandName}}\n- Objective: {{objective}}\n- Audience: {{audience}}\n- Tone: {{tone}}\n- Language: {{language}}\n- Key Message: {{keyMessage}}\n- CTA: {{cta}}\n- Format: {{format}}\n\nRequirements:\n- Use natural spoken language\n- Include sound cues if helpful (SFX)\n- Structure: Hook -> Message -> CTA\n- Make it engaging within first 5 seconds', '{"type":"object","required":["script","voiceTone","structure","sfx"]}', '[{"Name":"duration","Description":"Ad duration in seconds","IsRequired":true,"DefaultValue":"30"},{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"objective","Description":"Objective","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"tone","Description":"Tone","IsRequired":true,"DefaultValue":"Energetic"},{"Name":"language","Description":"Language","IsRequired":true,"DefaultValue":"English"},{"Name":"keyMessage","Description":"Key message","IsRequired":true},{"Name":"cta","Description":"Call to action","IsRequired":true},{"Name":"format","Description":"Dialogue | SingleVoice | Testimonial | Promo","IsRequired":true,"DefaultValue":"SingleVoice"}]', 8.80, 0, 'creative-global-system', true),
('creative-brief-radio', 'Radio', 'Zulu', 1, 'v1.0', 'Create a structured isiZulu radio ad response.', 'Bhala i-ad yomsakazo yemizuzwana engu-{{duration}}.\n\nOkokufaka:\n- Igama le-brand: {{brandName}}\n- Injongo: {{objective}}\n- Izethameli: {{audience}}\n- Ithoni: {{tone}}\n- Ulimi: {{language}}\n- Umlayezo oyinhloko: {{keyMessage}}\n- CTA: {{cta}}\n- Ifomethi: {{format}}', '{"type":"object","required":["script","voiceTone","structure","sfx"]}', '[{"Name":"duration","Description":"Ad duration in seconds","IsRequired":true,"DefaultValue":"30"},{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"objective","Description":"Objective","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"tone","Description":"Tone","IsRequired":true,"DefaultValue":"Conversational"},{"Name":"language","Description":"Language","IsRequired":true,"DefaultValue":"Zulu"},{"Name":"keyMessage","Description":"Key message","IsRequired":true},{"Name":"cta","Description":"Call to action","IsRequired":true},{"Name":"format","Description":"Dialogue | SingleVoice | Testimonial | Promo","IsRequired":true,"DefaultValue":"SingleVoice"}]', 8.60, 0, 'creative-global-system', true),
('creative-brief-tv', 'Tv', 'English', 1, 'v1.0', 'Create a story-driven TV script response.', 'Create a TV commercial script.\n\nInputs:\n- Brand: {{brandName}}\n- Objective: {{objective}}\n- Audience: {{audience}}\n- Tone: {{tone}}\n\nRequirements:\n- Story-driven\n- Emotional engagement\n- Clear CTA at end', '{"type":"object","required":["duration","scenes","cta"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"objective","Description":"Objective","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"tone","Description":"Tone","IsRequired":true,"DefaultValue":"Balanced"},{"Name":"cta","Description":"Call to action","IsRequired":true}]', 8.70, 0, 'creative-global-system', true),
('creative-brief-billboard', 'Billboard', 'English', 1, 'v1.0', 'Create a concise billboard response.', 'Create a billboard advertisement.\n\nInputs:\n- Brand: {{brandName}}\n- Audience: {{audience}}\n- Location Context: {{location}}\n- Key Message: {{keyMessage}}\n- CTA: {{cta}}\n- Style: {{style}}\n\nSTRICT RULES:\n- Max 6 words headline\n- Must be readable in 3 seconds\n- No clutter\n- Focus on ONE idea', '{"type":"object","required":["headline","subtext","cta","visualDirection","designNotes"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"location","Description":"Location context","IsRequired":true},{"Name":"keyMessage","Description":"Key message","IsRequired":true},{"Name":"cta","Description":"Call to action","IsRequired":true},{"Name":"style","Description":"Minimal | Bold | Luxury | Street | Humorous","IsRequired":true,"DefaultValue":"Bold"}]', 8.90, 0, 'creative-global-system', true),
('creative-brief-newspaper', 'Newspaper', 'English', 1, 'v1.0', 'Create a newspaper ad response.', 'Create a newspaper advertisement.\n\nInputs:\n- Brand: {{brandName}}\n- Audience: {{audience}}\n- Offer: {{offer}}', '{"type":"object","required":["headline","body","cta","layoutSuggestion"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"offer","Description":"Offer","IsRequired":true},{"Name":"cta","Description":"Call to action","IsRequired":true}]', 8.40, 0, 'creative-global-system', true),
('creative-brief-digital-meta', 'Digital', 'English', 1, 'v1.0', 'Create Meta ad JSON output only.', 'Create a high-converting Meta ad.\n\nInputs:\n- Brand: {{brandName}}\n- Audience: {{audience}}\n- Objective: {{objective}}\n- Tone: {{tone}}\n\nRequirements:\n- Strong hook in first line\n- Conversational tone\n- Include emotional trigger', '{"type":"object","required":["primaryText","headline","cta","hook","variants"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"objective","Description":"Objective","IsRequired":true},{"Name":"tone","Description":"Tone","IsRequired":true,"DefaultValue":"Conversational"},{"Name":"cta","Description":"Call to action","IsRequired":true}]', 8.70, 0, 'creative-global-system', true),
('creative-brief-digital-tiktok', 'Digital', 'English', 1, 'v1.0', 'Create TikTok ad JSON output only.', 'Create a TikTok ad script.\n\nInputs:\n- Brand: {{brandName}}\n- Audience: {{audience}}\n- Tone: {{tone}}\n\nRequirements:\n- Hook within first 2 seconds\n- Native, not ad-like\n- Trend-aware style', '{"type":"object","required":["hook","script","sceneIdeas","duration"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"tone","Description":"Tone","IsRequired":true,"DefaultValue":"Energetic"}]', 8.60, 0, 'creative-global-system', true),
('creative-brief-digital-google', 'Digital', 'English', 1, 'v1.0', 'Create Google Ads JSON output only.', 'Create Google Search ads.\n\nInputs:\n- Brand: {{brandName}}\n- Objective: {{objective}}', '{"type":"object","required":["headlines","descriptions","keywords"]}', '[{"Name":"brandName","Description":"Brand name","IsRequired":true},{"Name":"objective","Description":"Objective","IsRequired":true}]', 8.50, 0, 'creative-global-system', true),
('creative-localisation', 'Digital', 'English', 1, 'v1.0', 'Adapt copy culturally for South Africa and return JSON only.', 'Adapt the following ad into {{language}} for a South African audience.\n\nInputs:\n- Original Content: {{content}}\n- Target Audience: {{audience}}\n- Tone: {{tone}}\n\nRequirements:\n- Do NOT translate directly\n- Localise slang and phrasing\n- Maintain intent and CTA', '{"type":"object","required":["localisedText","notes"]}', '[{"Name":"language","Description":"Target language","IsRequired":true},{"Name":"content","Description":"Original content","IsRequired":true},{"Name":"audience","Description":"Audience summary","IsRequired":true},{"Name":"tone","Description":"Tone","IsRequired":true}]', 8.80, 0, 'creative-global-system', true),
('creative-refinement', 'Digital', 'English', 1, 'v1.0', 'Improve ad copy based on explicit user feedback and return JSON only.', 'Improve this ad based on feedback.\n\nInputs:\n- Original Ad: {{creative}}\n- Feedback: {{feedback}}', '{"type":"object","required":["updatedVersion","improvements"]}', '[{"Name":"creative","Description":"Original creative JSON or text","IsRequired":true},{"Name":"feedback","Description":"Feedback text","IsRequired":true}]', 8.60, 0, 'creative-global-system', true),
('creative-qa-default', 'Digital', 'English', 1, 'v1.0', 'You are an ad QA evaluator. Return scorecard JSON only.', 'Score clarity, brand fit, emotional impact, and CTA strength.', '{"type":"object","required":["metrics","overall","issues","status"]}', '[]', 8.70, 0, 'creative-global-system', true)
on conflict (key, channel, language, version) do update
set system_prompt = excluded.system_prompt,
    template_prompt = excluded.template_prompt,
    output_schema = excluded.output_schema,
    variables_json = excluded.variables_json,
    version_label = excluded.version_label,
    performance_score = excluded.performance_score,
    usage_count = excluded.usage_count,
    base_system_prompt_key = excluded.base_system_prompt_key,
    is_active = excluded.is_active;

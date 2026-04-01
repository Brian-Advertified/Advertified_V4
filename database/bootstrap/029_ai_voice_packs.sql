create table if not exists ai_voice_packs (
    id uuid primary key default gen_random_uuid(),
    provider varchar(40) not null default 'ElevenLabs',
    name varchar(120) not null,
    accent varchar(80) null,
    language varchar(40) null,
    tone varchar(80) null,
    persona varchar(120) null,
    use_cases_json jsonb not null default '[]'::jsonb,
    voice_id varchar(120) not null,
    sample_audio_url text null,
    prompt_template text not null,
    pricing_tier varchar(20) not null default 'standard',
    is_active boolean not null default true,
    sort_order integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists uq_ai_voice_packs_provider_name
    on ai_voice_packs(provider, name);

create index if not exists ix_ai_voice_packs_provider_active_sort
    on ai_voice_packs(provider, is_active, sort_order);

insert into ai_voice_packs (provider, name, accent, language, tone, persona, use_cases_json, voice_id, sample_audio_url, prompt_template, pricing_tier, is_active, sort_order)
values
('ElevenLabs', 'Kasi Hustler', 'Township SA', 'English/Zulu mix', 'Energetic', 'Street-smart promoter', '["Promotions","Retail ads","Taxi rank marketing"]', 'hpp4J3VqNfWAUOO0d1Us', null, 'Speak like a young township hustler. Use South African slang. High energy, persuasive.', 'standard', true, 10),
('ElevenLabs', 'Metro FM Presenter', 'SA Urban', 'English', 'Smooth', 'Professional presenter', '["Radio ads","Brand campaigns"]', 'hpp4J3VqNfWAUOO0d1Us', null, 'Deliver clean South African English with smooth confidence, like a top radio presenter.', 'premium', true, 20),
('ElevenLabs', 'Corporate SA Professional', 'Neutral SA', 'English', 'Calm', 'Trusted advisor', '["Finance","Insurance","Corporate brand"]', 'hpp4J3VqNfWAUOO0d1Us', null, 'Speak with trustworthy, measured confidence for professional business audiences.', 'premium', true, 30),
('ElevenLabs', 'Afrikaans Friendly Voice', 'Afrikaans SA', 'Afrikaans/English', 'Warm', 'Community host', '["Retail","Local radio","Community offers"]', 'hpp4J3VqNfWAUOO0d1Us', null, 'Use warm, relatable Afrikaans-friendly phrasing while keeping the message clear and inviting.', 'standard', true, 40),
('ElevenLabs', 'Luxury Sandton Voice', 'Premium SA', 'English', 'Polished', 'Luxury narrator', '["High-end brands","Property","Wealth"]', 'hpp4J3VqNfWAUOO0d1Us', null, 'Deliver polished premium South African English with exclusivity, clarity, and confidence.', 'exclusive', true, 50)
on conflict (provider, name) do update
set
    accent = excluded.accent,
    language = excluded.language,
    tone = excluded.tone,
    persona = excluded.persona,
    use_cases_json = excluded.use_cases_json,
    voice_id = excluded.voice_id,
    sample_audio_url = excluded.sample_audio_url,
    prompt_template = excluded.prompt_template,
    pricing_tier = excluded.pricing_tier,
    is_active = excluded.is_active,
    sort_order = excluded.sort_order,
    updated_at = now();


create table if not exists ai_voice_prompt_templates
(
    id uuid primary key default gen_random_uuid(),
    template_number integer not null unique,
    category text not null,
    name text not null,
    prompt_template text not null,
    primary_voice_pack_name text not null,
    fallback_voice_pack_names_json jsonb not null default '[]'::jsonb,
    is_active boolean not null default true,
    sort_order integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_ai_voice_prompt_templates_category
    on ai_voice_prompt_templates (category);

create index if not exists ix_ai_voice_prompt_templates_active_sort
    on ai_voice_prompt_templates (is_active, sort_order, template_number);

insert into ai_voice_prompt_templates
    (template_number, category, name, prompt_template, primary_voice_pack_name, fallback_voice_pack_names_json, sort_order)
values
    (1, 'kasi_street', 'Promo Push', 'Speak like a young township hustler. High energy. Create a short ad for product: {product}. Audience: township consumers. Goal: drive immediate sales. Use South African slang like "sharp", "howzit", "don''t sleep on this". Make it urgent and exciting.', 'Kasi Hustler', '["Street Interview","Mzansi Comedic"]'::jsonb, 1),
    (2, 'kasi_street', 'Weekend Special', 'Sound like a loud taxi rank promoter. Promote a weekend special for {product}. Use urgency and repetition. Include "this weekend only" and "don''t miss out".', 'Taxi Rank Announcer', '["Kasi Hustler"]'::jsonb, 2),
    (3, 'kasi_street', 'Street Cred Brand', 'Sound like a respected kasi entrepreneur. Explain why {product} is trusted in the community. Keep it real, relatable, and confident.', 'Kasi Hustler', '["Street Interview"]'::jsonb, 3),
    (4, 'kasi_street', 'Flash Sale', 'Energetic street voice. Announce a flash sale for {product}. Keep under 20 seconds. Push urgency hard.', 'Taxi Rank Announcer', '["Kasi Hustler"]'::jsonb, 4),
    (5, 'kasi_street', 'New Store Opening', 'Excited township vibe. Announce a new store opening for {business}. Include location and invite people to come through.', 'Kasi Hustler', '["Street Interview"]'::jsonb, 5),
    (6, 'kasi_street', 'Word of Mouth Style', 'Casual conversation tone. Make it sound like one friend telling another about {product}. Natural slang, not scripted.', 'Street Interview', '["Kasi Hustler"]'::jsonb, 6),
    (7, 'kasi_street', 'Hustler Motivation', 'Motivational kasi tone. Position {product} as helping people hustle smarter.', 'Kasi Hustler', '["Mzansi Comedic"]'::jsonb, 7),
    (8, 'kasi_street', 'Price Drop Alert', 'Loud energetic tone. Announce price drop for {product}. Repeat key message twice.', 'Taxi Rank Announcer', '["Kasi Hustler"]'::jsonb, 8),
    (9, 'kasi_street', 'Taxi Rank Loop Ad', 'Loop-style announcement. Make it repetitive and catchy for taxi rank speakers.', 'Taxi Rank Announcer', '["Street Interview"]'::jsonb, 9),
    (10, 'kasi_street', 'Competition Giveaway', 'Excited hype tone. Promote a giveaway for {product}. Tell users how to enter.', 'Kasi Hustler', '["Mzansi Comedic"]'::jsonb, 10),

    (11, 'radio_professional', 'Classic Radio Ad', 'Sound like a Metro FM presenter. Create a polished 30-second ad for {product}. Clear, smooth delivery.', 'Metro FM Presenter', '["Corporate SA Professional","Luxury Sandton"]'::jsonb, 11),
    (12, 'radio_professional', 'Brand Awareness', 'Professional tone. Introduce {brand} and what it stands for. Focus on trust and credibility.', 'Corporate SA Professional', '["Metro FM Presenter"]'::jsonb, 12),
    (13, 'radio_professional', 'Storytelling Ad', 'Narrative radio style. Tell a short emotional story leading to {product}.', 'Metro FM Presenter', '["Corporate SA Professional"]'::jsonb, 13),
    (14, 'radio_professional', 'CTA Focus', 'Radio voice. Focus heavily on CTA for {product}. End strong.', 'Metro FM Presenter', '["Corporate SA Professional"]'::jsonb, 14),
    (15, 'radio_professional', 'Financial Services', 'Calm trustworthy tone. Explain benefits of {product}. Avoid slang.', 'Corporate SA Professional', '["Luxury Sandton"]'::jsonb, 15),
    (16, 'radio_professional', 'Insurance Ad', 'Reassuring tone. Make customer feel safe choosing {product}.', 'Corporate SA Professional', '["Metro FM Presenter"]'::jsonb, 16),
    (17, 'radio_professional', 'Interview Style', 'Host and guest style. Simulate discussion about {product}.', 'Metro FM Presenter', '["Corporate SA Professional"]'::jsonb, 17),
    (18, 'radio_professional', 'Testimonial Ad', 'Professional testimonial tone. Customer explains why they trust {product}.', 'Corporate SA Professional', '["Metro FM Presenter"]'::jsonb, 18),
    (19, 'radio_professional', 'Event Promotion', 'Radio host energy. Promote event {event_name}. Include date and location.', 'Metro FM Presenter', '["Kasi Hustler"]'::jsonb, 19),
    (20, 'radio_professional', 'Premium Brand', 'Smooth luxurious tone. Position {product} as premium.', 'Luxury Sandton Voice', '["Corporate SA Professional"]'::jsonb, 20),

    (21, 'comedy_viral', 'Funny Skit', 'Comedic tone. Create a funny scenario leading to {product}.', 'Mzansi Comedic Voice', '["Kasi Hustler","Street Interview Style"]'::jsonb, 21),
    (22, 'comedy_viral', 'Exaggeration Humor', 'Over-the-top humor. Make the problem huge, solution is {product}.', 'Mzansi Comedic Voice', '["Kasi Hustler"]'::jsonb, 22),
    (23, 'comedy_viral', 'Relatable Struggle', 'Funny real-life struggle. Tie it back to {product}.', 'Kasi Hustler', '["Mzansi Comedic Voice"]'::jsonb, 23),
    (24, 'comedy_viral', 'Friend Banter', 'Two friends joking. Keep it casual and funny around {product}.', 'Street Interview Style', '["Mzansi Comedic Voice"]'::jsonb, 24),
    (25, 'comedy_viral', 'Unexpected Twist', 'Start serious and end funny. Reveal {product} as solution.', 'Mzansi Comedic Voice', '["Street Interview Style"]'::jsonb, 25),
    (26, 'comedy_viral', 'Meme Style', 'Short punchy meme-inspired ad for {product}.', 'Kasi Hustler', '["Mzansi Comedic Voice"]'::jsonb, 26),
    (27, 'comedy_viral', 'Sarcastic Tone', 'Dry humor. Subtly promote {product}.', 'Mzansi Comedic Voice', '["Street Interview Style"]'::jsonb, 27),
    (28, 'comedy_viral', 'Parent vs Youth', 'Funny generational difference around {product}.', 'Mzansi Comedic Voice', '["Kasi Hustler"]'::jsonb, 28),
    (29, 'comedy_viral', 'Workplace Humor', 'Office scenario. Problem solved by {product}.', 'Mzansi Comedic Voice', '["Corporate SA Professional"]'::jsonb, 29),
    (30, 'comedy_viral', 'Dramatic Overreaction', 'Overdramatic entertaining voice to promote {product}.', 'Mzansi Comedic Voice', '["Kasi Hustler"]'::jsonb, 30),

    (31, 'corporate_trust', 'Corporate Intro', 'Professional voice. Explain company mission for {brand}.', 'Corporate SA Professional', '["Luxury Sandton Voice"]'::jsonb, 31),
    (32, 'corporate_trust', 'Investor Style', 'Confident data-driven voice. Explain value of {product}.', 'Corporate SA Professional', '["Luxury Sandton Voice"]'::jsonb, 32),
    (33, 'corporate_trust', 'B2B Pitch', 'Direct clear B2B pitch for {product}.', 'Corporate SA Professional', '["Luxury Sandton Voice"]'::jsonb, 33),
    (34, 'corporate_trust', 'LinkedIn Style', 'Thought-leadership tone. Position {brand} with authority.', 'Corporate SA Professional', '["Luxury Sandton Voice"]'::jsonb, 34),
    (35, 'corporate_trust', 'Case Study', 'Explain a success story and measurable outcomes for {product}.', 'Corporate SA Professional', '["Metro FM Presenter"]'::jsonb, 35),
    (36, 'corporate_trust', 'Product Demo', 'Step-by-step explanation of how {product} works.', 'Corporate SA Professional', '["SA Teacher"]'::jsonb, 36),
    (37, 'corporate_trust', 'FAQ Style', 'Answer common questions simply about {product}.', 'Corporate SA Professional', '["SA Teacher"]'::jsonb, 37),
    (38, 'corporate_trust', 'Authority Voice', 'Expert tone building credibility for {product}.', 'Corporate SA Professional', '["Community Leader Voice"]'::jsonb, 38),
    (39, 'corporate_trust', 'Compliance Friendly', 'Formal compliance-safe tone. Avoid exaggeration while promoting {product}.', 'Corporate SA Professional', '["Authority Voice"]'::jsonb, 39),
    (40, 'corporate_trust', 'Long-Form Explainer', 'Detailed educational explanation of {product}.', 'Corporate SA Professional', '["SA Teacher"]'::jsonb, 40),

    (41, 'multilingual_local', 'Zulu Promo', 'Speak in isiZulu. Promote {product}. Keep it natural and culturally grounded.', 'Zulu Native Narrator', '["Kasi Hustler"]'::jsonb, 41),
    (42, 'multilingual_local', 'Afrikaans Ad', 'Friendly Afrikaans tone. Promote {product} clearly and warmly.', 'Afrikaans Friendly Voice', '["Corporate SA Professional"]'::jsonb, 42),
    (43, 'multilingual_local', 'Code-Switching', 'Mix English and Zulu slang naturally while promoting {product}.', 'Kasi Hustler', '["Zulu Native Narrator"]'::jsonb, 43),
    (44, 'multilingual_local', 'Rural Community', 'Warm respectful tone and simple language to promote {product}.', 'Community Leader Voice', '["Zulu Native Narrator"]'::jsonb, 44),
    (45, 'multilingual_local', 'Government Campaign', 'Clear authoritative public message around {product}.', 'Authority Voice', '["Community Leader Voice"]'::jsonb, 45),
    (46, 'multilingual_local', 'Health Awareness', 'Caring tone. Explain importance and action steps for {product}.', 'Community Leader Voice', '["SA Teacher"]'::jsonb, 46),
    (47, 'multilingual_local', 'Education Campaign', 'Teacher-like tone. Clear and helpful explanation for {product}.', 'SA Teacher', '["Community Leader Voice"]'::jsonb, 47),
    (48, 'multilingual_local', 'Youth Campaign', 'Trendy youth tone for {product}.', 'Kasi Hustler', '["Mzansi Comedic Voice"]'::jsonb, 48),
    (49, 'multilingual_local', 'Local Pride', 'Celebrate South African identity and connect to {product}.', 'Metro FM Presenter', '["Community Leader Voice"]'::jsonb, 49),
    (50, 'multilingual_local', 'Community Leader', 'Respected trusted voice encouraging adoption of {product}.', 'Community Leader Voice', '["Corporate SA Professional"]'::jsonb, 50),

    (51, 'bonus', 'Urgent Alert', 'High urgency alert. Immediate action required for {product}.', 'Taxi Rank Announcer', '["Kasi Hustler"]'::jsonb, 51),
    (52, 'bonus', 'Soft Sell', 'Subtle persuasion with non-aggressive tone for {product}.', 'Corporate SA Professional', '["Metro FM Presenter"]'::jsonb, 52),
    (53, 'bonus', 'Retargeting Ad', 'Speak to someone who already saw the ad and push conversion for {product}.', 'Metro FM Presenter', '["Corporate SA Professional"]'::jsonb, 53),
    (54, 'bonus', 'Launch Hype', 'Big announcement energy for launch of {product}.', 'Metro FM Presenter', '["Kasi Hustler"]'::jsonb, 54),
    (55, 'bonus', 'Scarcity Play', 'Limited stock message to drive urgency for {product}.', 'Taxi Rank Announcer', '["Metro FM Presenter"]'::jsonb, 55)
on conflict (template_number) do update
set category = excluded.category,
    name = excluded.name,
    prompt_template = excluded.prompt_template,
    primary_voice_pack_name = excluded.primary_voice_pack_name,
    fallback_voice_pack_names_json = excluded.fallback_voice_pack_names_json,
    sort_order = excluded.sort_order,
    updated_at = now();

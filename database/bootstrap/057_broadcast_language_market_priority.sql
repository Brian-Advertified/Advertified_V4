create table if not exists broadcast_language_market_priority (
    language_code text primary key references ref_language(code),
    market_rank integer not null,
    is_active boolean not null default true,
    notes text null,
    updated_at timestamptz not null default now()
);

insert into broadcast_language_market_priority (language_code, market_rank, is_active, notes) values
    ('english', 1, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('isizulu', 2, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('afrikaans', 3, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('isixhosa', 4, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('setswana', 5, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('sesotho', 6, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('sepedi', 7, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('xitsonga', 8, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('tshivenda', 9, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('siswati', 10, true, 'Default operator-managed market priority for multilingual radio and TV planning.'),
    ('isindebele', 11, true, 'Default operator-managed market priority for multilingual radio and TV planning.')
on conflict (language_code) do update
set market_rank = excluded.market_rank,
    is_active = excluded.is_active,
    notes = excluded.notes,
    updated_at = now();


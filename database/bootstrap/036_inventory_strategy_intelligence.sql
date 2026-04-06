ALTER TABLE media_outlet
    ADD COLUMN IF NOT EXISTS strategy_fit_json jsonb NOT NULL DEFAULT '{}'::jsonb;

DO $$
BEGIN
    ALTER TYPE user_role ADD VALUE IF NOT EXISTS 'creative_director';
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;

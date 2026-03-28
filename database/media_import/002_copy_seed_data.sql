\set ON_ERROR_STOP on

-- This script prepares the staging tables for a fresh load.
-- The actual CSV imports are executed by tools/media_import/run_media_import.ps1
-- via explicit \copy commands with resolved full file paths.

BEGIN;

TRUNCATE TABLE raw_import_pages RESTART IDENTITY CASCADE;
TRUNCATE TABLE import_manifest RESTART IDENTITY CASCADE;
TRUNCATE TABLE package_document_metadata RESTART IDENTITY CASCADE;
TRUNCATE TABLE inventory_items RESTART IDENTITY CASCADE;
TRUNCATE TABLE radio_packages RESTART IDENTITY CASCADE;
TRUNCATE TABLE radio_slot_grids RESTART IDENTITY CASCADE;
TRUNCATE TABLE sabc_rate_tables RESTART IDENTITY CASCADE;

COMMIT;

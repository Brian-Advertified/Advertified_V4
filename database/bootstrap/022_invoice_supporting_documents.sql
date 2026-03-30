ALTER TABLE invoices
    ADD COLUMN IF NOT EXISTS supporting_document_storage_object_key TEXT;

ALTER TABLE invoices
    ADD COLUMN IF NOT EXISTS supporting_document_file_name VARCHAR(255);

ALTER TABLE invoices
    ADD COLUMN IF NOT EXISTS supporting_document_uploaded_at_utc TIMESTAMP NULL;

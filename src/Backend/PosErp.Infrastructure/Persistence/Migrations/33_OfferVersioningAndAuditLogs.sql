-- Migration 33: Offer Versioning, Audit Logs, and Offer Usage Log Enhancements
-- Fixes: 42P01: relation "offer_versions" does not exist (POS billing with offers)

-- 1. Create offer_versions table (tracks history of offer changes)
CREATE TABLE IF NOT EXISTS offer_versions (
    id UUID PRIMARY KEY,
    offer_id UUID NOT NULL,
    version_number INT NOT NULL DEFAULT 1,
    modified_by UUID,
    modified_date TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    previous_configuration TEXT NOT NULL DEFAULT '{}',
    change_reason TEXT NOT NULL DEFAULT 'Initial Creation'
);

CREATE INDEX IF NOT EXISTS idx_offer_versions_offer_id ON offer_versions(offer_id);
CREATE INDEX IF NOT EXISTS idx_offer_versions_version ON offer_versions(offer_id, version_number DESC);

-- 2. Create audit_logs table (system-wide audit trail)
CREATE TABLE IF NOT EXISTS audit_logs (
    id UUID PRIMARY KEY,
    user_id UUID,
    user_name TEXT,
    action TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    entity_id TEXT,
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ip_address TEXT,
    details TEXT
);

CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON audit_logs(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user ON audit_logs(user_id);

-- 3. Add missing columns to offer_usage_logs (if they don't already exist)
ALTER TABLE offer_usage_logs
    ADD COLUMN IF NOT EXISTS offer_version INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS original_cart_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS final_cart_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS terminal_name TEXT NOT NULL DEFAULT '';

-- 4. Seed an initial version record for all existing offers (if any)
INSERT INTO offer_versions (id, offer_id, version_number, modified_date, previous_configuration, change_reason)
SELECT 
    gen_random_uuid(),
    id,
    1,
    created_at,
    '{"note":"Seeded from existing offer"}',
    'Initial seed from migration 33'
FROM offers
WHERE id NOT IN (SELECT offer_id FROM offer_versions)
ON CONFLICT DO NOTHING;

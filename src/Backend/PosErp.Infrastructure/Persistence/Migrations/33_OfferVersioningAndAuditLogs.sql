-- Migration 33: Offer Versioning, Audit Logs, and Offer Usage Log Enhancements
-- Fixes: 42P01: relation "offer_usage_logs" does not exist (POS billing with offers)

-- 1. Create offer_usage_logs base table (if it doesn't already exist from EF migrations)
CREATE TABLE IF NOT EXISTS offer_usage_logs (
    id UUID PRIMARY KEY,
    offer_id UUID NOT NULL,
    offer_name TEXT NOT NULL,
    invoice_id UUID NOT NULL,
    invoice_number TEXT NOT NULL,
    invoice_date TIMESTAMP WITH TIME ZONE NOT NULL,
    customer_id UUID,
    terminal_id UUID NOT NULL,
    cashier_id UUID NOT NULL,
    store_id UUID,
    discount_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    revenue_influenced NUMERIC(18,2) NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- 2. Create offer_versions table (tracks history of offer changes)
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

-- 3. Create audit_logs table (system-wide audit trail)
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

-- Ensure columns exist if table was already created by migration 14
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS user_name TEXT;
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS entity_type TEXT NOT NULL DEFAULT '';
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS details TEXT;

CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON audit_logs(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user ON audit_logs(user_id);

-- 4. Add missing columns to offer_usage_logs (if they don't already exist)
ALTER TABLE offer_usage_logs
    ADD COLUMN IF NOT EXISTS offer_version INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS original_cart_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS final_cart_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS terminal_name TEXT NOT NULL DEFAULT '';

-- 5. Seed an initial version record for all existing offers (if any)
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

-- Migration 39: Fix audit_logs schema defect from migration 33
-- Migration 33 used CREATE TABLE IF NOT EXISTS audit_logs, which was a no-op
-- on existing databases because migration 14 had already created the table.
-- As a result, the columns 'timestamp', 'entity_type', 'user_name', and 'details'
-- were never added, causing runtime failures and preventing the CREATE INDEX
-- statements in migration 33 from succeeding on clean schema builds.
--
-- This migration ensures the columns exist and creates the missing indexes.

ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS user_name TEXT;
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS entity_type TEXT NOT NULL DEFAULT '';
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS details TEXT;

CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON audit_logs(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user ON audit_logs(user_id);

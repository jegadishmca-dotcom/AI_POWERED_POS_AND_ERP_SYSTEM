-- ==============================================================================
-- ADD EXPIRY ALERT THRESHOLD TO EMAIL SETTINGS
-- ==============================================================================

ALTER TABLE email_settings
ADD COLUMN IF NOT EXISTS expiry_alert_threshold_days INTEGER NOT NULL DEFAULT 30;

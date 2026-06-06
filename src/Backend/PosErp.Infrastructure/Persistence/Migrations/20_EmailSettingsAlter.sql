-- ==============================================================================
-- ADD MISSING EMAIL SETTING COLUMNS
-- ==============================================================================

ALTER TABLE email_settings 
ADD COLUMN IF NOT EXISTS delivery_method VARCHAR(20) DEFAULT 'POSTMARK',
ADD COLUMN IF NOT EXISTS mailgun_domain VARCHAR(255) DEFAULT '',
ADD COLUMN IF NOT EXISTS mailgun_api_key TEXT DEFAULT '',
ADD COLUMN IF NOT EXISTS postmark_token TEXT DEFAULT '',
ADD COLUMN IF NOT EXISTS resend_api_key TEXT DEFAULT '',
ADD COLUMN IF NOT EXISTS expiry_alert_threshold_days INT DEFAULT 30;

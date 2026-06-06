-- ==============================================================================
-- ADD EMAIL API SUPPORT (MAILGUN & POSTMARK)
-- ==============================================================================

ALTER TABLE email_settings
ADD COLUMN IF NOT EXISTS delivery_method VARCHAR(50) NOT NULL DEFAULT 'POSTMARK',
ADD COLUMN IF NOT EXISTS mailgun_domain VARCHAR(255) NULL,
ADD COLUMN IF NOT EXISTS mailgun_api_key TEXT NULL,
ADD COLUMN IF NOT EXISTS postmark_token TEXT NULL;

-- Set existing global row default delivery method to Postmark
UPDATE email_settings
SET delivery_method = 'POSTMARK'
WHERE id = 'global';

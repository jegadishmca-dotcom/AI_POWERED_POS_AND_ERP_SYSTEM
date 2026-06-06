-- ==============================================================================
-- ADD RESEND EMAIL API SUPPORT
-- ==============================================================================

ALTER TABLE email_settings
ADD COLUMN IF NOT EXISTS resend_api_key TEXT NULL;

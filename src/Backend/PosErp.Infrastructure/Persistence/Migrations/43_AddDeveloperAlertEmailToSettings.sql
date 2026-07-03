ALTER TABLE email_settings ADD COLUMN IF NOT EXISTS developer_alert_email VARCHAR(255) DEFAULT '';

-- ==============================================================================
-- EMAIL SETTINGS SCHEMA
-- ==============================================================================

CREATE TABLE IF NOT EXISTS email_settings (
    id VARCHAR(50) PRIMARY KEY,
    smtp_server VARCHAR(255) NOT NULL,
    smtp_port INT NOT NULL,
    sender_email VARCHAR(255) NOT NULL,
    sender_password TEXT NOT NULL,
    recipient_email VARCHAR(255) NOT NULL,
    enable_ssl BOOLEAN NOT NULL,
    trigger_interval_minutes INT NOT NULL DEFAULT 0
);

-- Seed initial email configuration
INSERT INTO email_settings (id, smtp_server, smtp_port, sender_email, sender_password, recipient_email, enable_ssl, trigger_interval_minutes)
VALUES ('global', 'smtp.gmail.com', 587, 'fortabletuse999@gmail.com', '', 'jegadishmca@gmail.com', TRUE, 0)
ON CONFLICT (id) DO NOTHING;

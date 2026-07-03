-- Migration 41: Add database metadata table for UAT/LIVE split safety checks.
CREATE TABLE IF NOT EXISTS database_metadata (
    id INT PRIMARY KEY DEFAULT 1,
    database_name VARCHAR(255) NOT NULL,
    environment_mode VARCHAR(50) NOT NULL, -- 'LIVE' or 'UAT'
    tenant_id UUID NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT chk_single_row CHECK (id = 1)
);

-- Seed initial default metadata row for the default database to pass startup checks
INSERT INTO database_metadata (id, database_name, environment_mode, tenant_id)
VALUES (1, 'posdb', 'LIVE', '00000000-0000-0000-0000-000000000000')
ON CONFLICT (id) DO NOTHING;

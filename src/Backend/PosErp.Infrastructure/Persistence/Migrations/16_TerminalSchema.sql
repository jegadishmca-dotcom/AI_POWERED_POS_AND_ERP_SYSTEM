-- ==============================================================================
-- TERMINALS SCHEMA
-- ==============================================================================

CREATE TABLE IF NOT EXISTS terminals (
    id UUID PRIMARY KEY,
    terminal_code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Seed initial terminal if not exists
INSERT INTO terminals (id, terminal_code, name, is_active, created_at)
VALUES ('00000000-0000-0000-0000-000000000001', 'TERMINAL 01', 'Counter 01 Main', TRUE, CURRENT_TIMESTAMP)
ON CONFLICT (terminal_code) DO NOTHING;

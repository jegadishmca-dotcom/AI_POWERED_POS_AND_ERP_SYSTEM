-- ==============================================================================
-- POS SESSIONS SCHEMA
-- ==============================================================================

CREATE TABLE IF NOT EXISTS pos_sessions (
    id UUID PRIMARY KEY,
    terminal_id UUID NOT NULL,
    cashier_id UUID NOT NULL,
    start_time TIMESTAMP WITH TIME ZONE NOT NULL,
    end_time TIMESTAMP WITH TIME ZONE,
    opening_float_cash NUMERIC(18, 2) NOT NULL DEFAULT 0,
    expected_closing_cash NUMERIC(18, 2) NOT NULL DEFAULT 0,
    actual_closing_cash NUMERIC(18, 2) NOT NULL DEFAULT 0,
    difference NUMERIC(18, 2) NOT NULL DEFAULT 0,
    status VARCHAR(20) NOT NULL DEFAULT 'OPEN'
);

CREATE INDEX IF NOT EXISTS idx_pos_sessions_terminal_status ON pos_sessions(terminal_id, cashier_id, status);

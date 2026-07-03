-- Migration 42: Create toggle_lockout_state table for live environment toggle lockout tracking.
CREATE TABLE IF NOT EXISTS toggle_lockout_state (
    account_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    failed_count INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMP WITH TIME ZONE NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

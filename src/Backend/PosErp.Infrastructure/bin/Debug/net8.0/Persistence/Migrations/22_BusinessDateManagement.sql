-- ==============================================================================
-- BUSINESS DATE & END-OF-DAY LOCK SCHEMA
-- ==============================================================================

CREATE TABLE IF NOT EXISTS store_business_dates (
    store_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    business_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'OPEN',
    opened_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    opened_by UUID NULL,
    closed_at TIMESTAMP WITH TIME ZONE NULL,
    closed_by UUID NULL,
    PRIMARY KEY (store_id, business_date)
);

-- Seed current calendar date as initial open business date to avoid blocking
INSERT INTO store_business_dates (store_id, business_date, status, opened_at)
VALUES ('00000000-0000-0000-0000-000000000000', CURRENT_DATE, 'OPEN', NOW())
ON CONFLICT (store_id, business_date) DO NOTHING;

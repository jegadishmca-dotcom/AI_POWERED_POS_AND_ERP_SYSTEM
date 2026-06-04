-- ==============================================================================
-- PHASE 3: OFFER ENHANCEMENTS
-- ==============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='offers' AND column_name='promo_code') THEN
        ALTER TABLE offers ADD COLUMN promo_code VARCHAR(50) UNIQUE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='offers' AND column_name='is_exclusive') THEN
        ALTER TABLE offers ADD COLUMN is_exclusive BOOLEAN DEFAULT FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='offers' AND column_name='max_usage_per_invoice') THEN
        ALTER TABLE offers ADD COLUMN max_usage_per_invoice INT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_offers_promocode ON offers(promo_code);

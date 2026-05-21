-- ==============================================================================
-- PHASE 3: OFFER ENHANCEMENTS
-- ==============================================================================
ALTER TABLE offers 
ADD COLUMN promo_code VARCHAR(50) UNIQUE,
ADD COLUMN is_exclusive BOOLEAN DEFAULT FALSE,
ADD COLUMN max_usage_per_invoice INT;

CREATE INDEX IF NOT EXISTS idx_offers_promocode ON offers(promo_code);

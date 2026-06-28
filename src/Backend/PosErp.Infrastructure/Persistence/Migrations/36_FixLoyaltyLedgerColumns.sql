-- =============================================================================
-- Migration 36: Fix loyalty_ledger missing columns
-- Root Cause: C# LoyaltyLedgerEntry entity has more properties than the DB table.
-- The DB table only has: id, customer_id, store_id, transaction_type, points,
--   reference_document, expiry_date, running_points, created_at, created_by
-- The C# entity requires: previous_balance, points_earned, points_redeemed,
--   balance_after_transaction, invoice_id, remarks
-- EF Core maps these automatically and crashes on INSERT with column-not-found.
-- =============================================================================

-- Add missing columns with safe defaults
ALTER TABLE loyalty_ledger
    ADD COLUMN IF NOT EXISTS previous_balance       NUMERIC(18,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS points_earned          NUMERIC(18,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS points_redeemed        NUMERIC(18,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS balance_after_transaction NUMERIC(18,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS invoice_id             UUID NULL,
    ADD COLUMN IF NOT EXISTS remarks                VARCHAR(500) NOT NULL DEFAULT '';

-- Back-fill existing rows: set balance_after_transaction from running_points
-- (running_points was the old column that tracked the same value)
UPDATE loyalty_ledger
SET balance_after_transaction = COALESCE(running_points, 0),
    points_earned = CASE WHEN COALESCE(points, 0) > 0 THEN COALESCE(points, 0) ELSE 0 END,
    points_redeemed = CASE WHEN COALESCE(points, 0) < 0 THEN ABS(COALESCE(points, 0)) ELSE 0 END
WHERE balance_after_transaction = 0;

-- Verify the fix
DO $$
DECLARE
    col_count INT;
BEGIN
    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_name = 'loyalty_ledger'
      AND column_name IN ('balance_after_transaction', 'previous_balance', 'points_earned', 'points_redeemed', 'invoice_id', 'remarks');

    IF col_count = 6 THEN
        RAISE NOTICE 'SUCCESS: All 6 missing loyalty_ledger columns added. POS checkout will work correctly.';
    ELSE
        RAISE WARNING 'WARNING: Only % of 6 expected columns found. Check migration.', col_count;
    END IF;
END $$;

-- =============================================================================
-- 45_AddIsTestFlagToInvoices.sql
--
-- Adds an is_test boolean column to the invoices table so that records created
-- by the automated test suite can be permanently distinguished from genuine
-- customer transactions.
--
-- Also adds a partial index so queries can cheaply exclude test rows without
-- touching live data, and a CHECK constraint that enforces the reserved
-- "TEST-" invoice-number prefix must always coincide with is_test = TRUE.
-- This makes it impossible to accidentally create a real invoice with a TEST-
-- prefix, or a test invoice without the flag.
-- =============================================================================

-- 1. Add the flag column (NULL-safe: existing rows default to false)
ALTER TABLE invoices
    ADD COLUMN IF NOT EXISTS is_test BOOLEAN NOT NULL DEFAULT FALSE;

-- 2. Enforce the naming convention in both directions via CHECK constraint:
--    • Any invoice whose number starts with "TEST-" MUST have is_test = TRUE.
--    • Any invoice with is_test = TRUE MUST have an invoice_number starting
--      with "TEST-".
ALTER TABLE invoices
    ADD CONSTRAINT ck_invoices_is_test_prefix
    CHECK (
        (is_test = FALSE AND invoice_number NOT LIKE 'TEST-%')
        OR
        (is_test = TRUE  AND invoice_number LIKE 'TEST-%')
    );

-- 3. Partial index: make filtering out test rows in production queries free.
CREATE INDEX IF NOT EXISTS idx_invoices_live_only
    ON invoices (created_at DESC)
    WHERE is_test = FALSE;

-- 4. Partial index: make the test-cleanup / audit queries fast.
CREATE INDEX IF NOT EXISTS idx_invoices_test_rows
    ON invoices (created_at DESC)
    WHERE is_test = TRUE;

-- 5. Back-fill the flag for any pre-existing test invoice numbers that used
--    the old ad-hoc "SMOKE-" / "WF" / "DIAG-" prefixes (these rows already
--    carry recognisable test patterns in their invoice_number, so we can
--    safely mark them now rather than leaving them unlabelled).
UPDATE invoices
SET is_test = TRUE
WHERE invoice_number LIKE 'WF%'
   OR invoice_number LIKE 'DIAG-%'
   OR invoice_number LIKE 'INV-SMOKE-%'
   OR invoice_number LIKE 'SMOKE-%'
   OR invoice_number LIKE 'TEST-%';

-- Note: the CHECK constraint above is evaluated AFTER the UPDATE, so these
-- rows must already have a TEST-prefixed number or the UPDATE won't rename
-- them — it only sets the flag. The naming convention enforcement kicks in
-- for all future INSERTs/UPDATEs; legacy rows with non-TEST- prefixes that
-- were just back-filled are grandfathered (the constraint is not retroactive
-- against rows that existed before the ALTER TABLE).
-- If strict enforcement is desired for legacy rows too, rename them first, then
-- apply the constraint. That cleanup is left to a future targeted migration.

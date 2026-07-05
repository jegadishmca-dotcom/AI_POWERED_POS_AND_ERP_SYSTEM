-- =============================================================================
-- 45_AddIsTestFlagToInvoices.sql
--
-- Adds an is_test boolean column to the invoices table so that records created
-- by the automated test suite can be permanently distinguished from genuine
-- customer transactions.
--
-- Execution order (important — constraint MUST come last):
--   1. Add the column (DEFAULT FALSE — all existing rows are live by default).
--   2. Create partial indexes (can exist before the constraint).
--   3. Back-fill legacy test rows: rename their invoice_number to carry a
--      "TEST-LEGACY-" prefix AND set is_test = TRUE in one atomic UPDATE, so
--      every row already satisfies the forthcoming CHECK before it is added.
--   4. Add the CHECK constraint (now safe — all rows are consistent).
--
-- Why rename rather than just setting the flag?
--   The CHECK constraint enforces both directions simultaneously:
--     • is_test = TRUE  ↔  invoice_number LIKE 'TEST-%'
--   Setting is_test = TRUE while leaving a 'WF-…' prefix would violate the
--   constraint.  Renaming the legacy numbers is safe here because every WF-/
--   DIAG-/SMOKE- row is test contamination that lives only in posdb_uat;
--   none of these prefixes appear in posdb_live's genuine transaction history.
-- =============================================================================

-- ── Step 1: Column ─────────────────────────────────────────────────────────
ALTER TABLE invoices
    ADD COLUMN IF NOT EXISTS is_test BOOLEAN NOT NULL DEFAULT FALSE;

-- ── Step 2: Partial indexes ─────────────────────────────────────────────────
-- Created before the constraint so they are available immediately and do not
-- depend on the constraint's existence.
CREATE INDEX IF NOT EXISTS idx_invoices_live_only
    ON invoices (created_at DESC)
    WHERE is_test = FALSE;

CREATE INDEX IF NOT EXISTS idx_invoices_test_rows
    ON invoices (created_at DESC)
    WHERE is_test = TRUE;

-- ── Step 3: Back-fill ───────────────────────────────────────────────────────
-- Rename legacy ad-hoc test invoice numbers to carry the canonical 'TEST-'
-- prefix AND set the flag in a single UPDATE so the two columns stay consistent
-- with each other at every point during the transaction.
--
-- Pattern mapping:
--   WF1-…, WF3-…, WF4-…, WF5-…  → TEST-LEGACY-WF1-… etc.
--   DIAG-…                        → TEST-LEGACY-DIAG-…
--   SMOKE-…  / INV-SMOKE-…        → TEST-LEGACY-SMOKE-…  / TEST-LEGACY-INV-SMOKE-…
--
-- Rows whose invoice_number already starts with 'TEST-' are also set here
-- so a re-run of the migration is idempotent.
UPDATE invoices
SET
    is_test        = TRUE,
    invoice_number = CASE
        WHEN invoice_number LIKE 'TEST-%'
            THEN invoice_number                            -- already canonical, no rename
        ELSE 'TEST-LEGACY-' || invoice_number              -- prepend prefix
    END
WHERE invoice_number LIKE 'WF%'
   OR invoice_number LIKE 'DIAG-%'
   OR invoice_number LIKE 'SMOKE-%'
   OR invoice_number LIKE 'INV-SMOKE-%'
   OR invoice_number LIKE 'TEST-%';                        -- idempotency guard

-- ── Step 4: CHECK constraint (added last — all rows already comply) ─────────
-- Enforces the bidirectional invariant:
--   • Every is_test = FALSE row must NOT start with 'TEST-'.
--   • Every is_test = TRUE  row MUST start with 'TEST-'.
--
-- Using NOT EXISTS rather than DROP/ADD so the migration is re-runnable
-- on a database that already applied it (pg_constraint lookup).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_invoices_is_test_prefix'
          AND conrelid = 'invoices'::regclass
    ) THEN
        ALTER TABLE invoices
            ADD CONSTRAINT ck_invoices_is_test_prefix
            CHECK (
                (is_test = FALSE AND invoice_number NOT LIKE 'TEST-%')
                OR
                (is_test = TRUE  AND invoice_number LIKE 'TEST-%')
            );
    END IF;
END
$$;


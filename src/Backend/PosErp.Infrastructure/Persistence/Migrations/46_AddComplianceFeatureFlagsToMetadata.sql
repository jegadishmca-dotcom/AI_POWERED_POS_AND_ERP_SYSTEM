-- =============================================================================
-- 46_AddComplianceFeatureFlagsToMetadata.sql
--
-- Adds EInvoiceEnabled and EWayBillEnabled boolean columns to database_metadata
-- so that compliance feature toggles survive container rebuilds (unlike
-- appsettings.json which is baked into the Docker image and reverts on rebuild).
--
-- Both default to FALSE — explicit opt-in required before any IRP/e-way bill
-- call is wired in.
-- =============================================================================

ALTER TABLE database_metadata
    ADD COLUMN IF NOT EXISTS einvoice_enabled  BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS ewaybill_enabled  BOOLEAN NOT NULL DEFAULT FALSE;

-- Ensure any existing row has the correct default
UPDATE database_metadata
SET einvoice_enabled = FALSE,
    ewaybill_enabled = FALSE
WHERE einvoice_enabled IS NULL
   OR ewaybill_enabled IS NULL;

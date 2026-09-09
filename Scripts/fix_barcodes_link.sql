-- ==============================================================================
-- Apple Supermarket POS & ERP System
-- Barcode Relinking & Activation SQL Script
-- Fixes barcode scanning in POS Billing & populates Barcode Value in Product Catalog
-- ==============================================================================

BEGIN;

-- 1. Deduplicate identical barcodes keeping the newest one (prevent unique constraint violation)
DELETE FROM barcodes a 
USING barcodes b
WHERE a.id < b.id 
  AND a.barcode = b.barcode;

-- 2. Update store_id to default store UUID if NULL
UPDATE barcodes 
SET store_id = '00000000-0000-0000-0000-000000000000'
WHERE store_id IS NULL;

-- 3. Re-link barcodes that are currently attached to soft-deleted old products
--    to the active, live product records sharing the exact same product_code
UPDATE barcodes b
SET product_id = new_p.id,
    is_deleted = false
FROM products old_p
JOIN products new_p ON old_p.product_code = new_p.product_code
WHERE b.product_id = old_p.id
  AND old_p.is_deleted = true
  AND new_p.is_deleted = false;

-- 4. Mark all re-linked barcodes as not deleted
UPDATE barcodes
SET is_deleted = false
WHERE is_deleted = true
  AND product_id IN (SELECT id FROM products WHERE is_deleted = false);

COMMIT;

-- Verification Summary Query
SELECT 
    'Active Barcodes Ready for Scanning' AS metric,
    COUNT(*)::text AS count
FROM barcodes b
JOIN products p ON b.product_id = p.id
WHERE p.is_deleted = false;

-- 20260920_backfill_store_id.sql
-- One-time reviewed migration to backfill NULL store_id records to Head Office (STORE-01)
-- Verified origin: All orphaned records were created at Head Office / Counter 01 Main.

BEGIN;

UPDATE purchase_order_headers 
SET store_id = '00000000-0000-0000-0000-000000000000' 
WHERE store_id IS NULL;

UPDATE grn_headers 
SET store_id = '00000000-0000-0000-0000-000000000000' 
WHERE store_id IS NULL;

UPDATE purchase_bill_headers 
SET store_id = '00000000-0000-0000-0000-000000000000' 
WHERE store_id IS NULL;

UPDATE invoices 
SET store_id = '00000000-0000-0000-0000-000000000000' 
WHERE store_id IS NULL;

UPDATE stock_adjustments 
SET store_id = '00000000-0000-0000-0000-000000000000' 
WHERE store_id IS NULL;

COMMIT;

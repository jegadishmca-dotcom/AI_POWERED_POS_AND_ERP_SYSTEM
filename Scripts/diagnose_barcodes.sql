-- ═══════════════════════════════════════════════════════════
-- DIAGNOSTIC: Check why these 5 barcodes are not scanning
-- Run: docker exec -i pos_postgres psql -U posadmin -d posdb_uat < Scripts/diagnose_barcodes.sql
-- ═══════════════════════════════════════════════════════════

-- 1. Check if these barcodes exist in ProductBarcodes table
SELECT '=== CHECK 1: Do these barcodes exist in ProductBarcodes? ===' AS diagnostic;
SELECT 
    b."BarcodeValue",
    b."Id" AS barcode_id,
    b."ProductId",
    b."IsActive",
    b."Type",
    b."CreatedAt"
FROM "ProductBarcodes" b
WHERE b."BarcodeValue" IN (
    '8901063365131',
    '8901063368590',
    '8901063025509',
    '8901063365933',
    '8901063012998'
);

-- 2. Check if these barcodes appear anywhere as raw text in Products table
SELECT '=== CHECK 2: Do these barcodes appear in Products.Barcode field? ===' AS diagnostic;
SELECT 
    p."Id" AS product_id,
    p."Name",
    p."Barcode",
    p."SKU",
    p."IsActive"
FROM "Products" p
WHERE p."Barcode" IN (
    '8901063365131',
    '8901063368590',
    '8901063025509',
    '8901063365933',
    '8901063012998'
);

-- 3. Partial match search - maybe the barcode has extra characters/spaces
SELECT '=== CHECK 3: Fuzzy/partial match in ProductBarcodes ===' AS diagnostic;
SELECT 
    b."BarcodeValue",
    b."ProductId",
    b."IsActive",
    length(b."BarcodeValue") AS barcode_length
FROM "ProductBarcodes" b
WHERE b."BarcodeValue" LIKE '%8901063365131%'
   OR b."BarcodeValue" LIKE '%8901063368590%'
   OR b."BarcodeValue" LIKE '%8901063025509%'
   OR b."BarcodeValue" LIKE '%8901063365933%'
   OR b."BarcodeValue" LIKE '%8901063012998%';

-- 4. Partial match in Products.Barcode field
SELECT '=== CHECK 4: Fuzzy/partial match in Products.Barcode ===' AS diagnostic;
SELECT 
    p."Id",
    p."Name",
    p."Barcode",
    length(p."Barcode") AS barcode_length
FROM "Products" p
WHERE p."Barcode" LIKE '%8901063365131%'
   OR p."Barcode" LIKE '%8901063368590%'
   OR p."Barcode" LIKE '%8901063025509%'
   OR p."Barcode" LIKE '%8901063365933%'
   OR p."Barcode" LIKE '%8901063012998%';

-- 5. Check if the Products linked to any matching barcodes are active
SELECT '=== CHECK 5: Overall ProductBarcodes stats ===' AS diagnostic;
SELECT 
    COUNT(*) AS total_barcodes,
    COUNT(*) FILTER (WHERE "IsActive" = true) AS active_barcodes,
    COUNT(*) FILTER (WHERE "IsActive" = false) AS inactive_barcodes,
    COUNT(*) FILTER (WHERE "ProductId" IS NULL) AS orphaned_barcodes
FROM "ProductBarcodes";

-- 6. Check how the POS barcode lookup works - search in the active barcode index
SELECT '=== CHECK 6: Active barcode scan simulation ===' AS diagnostic;
SELECT 
    b."BarcodeValue",
    b."IsActive" AS barcode_active,
    p."Id" AS product_id,
    p."Name" AS product_name,
    p."IsActive" AS product_active,
    p."SKU"
FROM "ProductBarcodes" b
LEFT JOIN "Products" p ON b."ProductId" = p."Id"
WHERE b."BarcodeValue" IN (
    '8901063365131',
    '8901063368590',
    '8901063025509',
    '8901063365933',
    '8901063012998'
)
AND b."IsActive" = true
AND p."IsActive" = true;

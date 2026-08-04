# Verification script for migrated posdb_uat database on PostgreSQL
param (
    [string]$Container = "pos_postgres",
    [string]$DbName = "posdb_uat",
    [string]$User = "posadmin"
)

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " VERIFYING MIGRATED DATA IN $DbName" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$sqlVerifications = @"
SELECT '1. PRODUCTS TOTAL' AS Metric, COUNT(*)::text AS Value FROM products
UNION ALL
SELECT '2. WEIGHABLE (KGS) PRODUCTS', COUNT(*)::text FROM products WHERE is_weighable = true
UNION ALL
SELECT '3. BARCODES TOTAL', COUNT(*)::text FROM barcodes
UNION ALL
SELECT '4. SUPPLIERS TOTAL', COUNT(*)::text FROM suppliers
UNION ALL
SELECT '5. PRODUCTS WITH PREFERRED SUPPLIER', COUNT(*)::text FROM products WHERE preferred_supplier_id IS NOT NULL
UNION ALL
SELECT '6. CUSTOMERS TOTAL', COUNT(*)::text FROM customers
UNION ALL
SELECT '7. CUSTOMERS WITH LOYALTY POINTS (>0)', COUNT(*)::text FROM customers WHERE running_loyalty_points > 0
UNION ALL
SELECT '8. TOTAL LOYALTY POINTS SUM', ROUND(SUM(running_loyalty_points), 2)::text FROM customers
UNION ALL
SELECT '9. TOTAL CUSTOMER LEDGER BALANCE SUM', ROUND(SUM(running_wallet_balance), 2)::text FROM customers
UNION ALL
SELECT '10. PRODUCT STOCK BATCHES TOTAL', COUNT(*)::text FROM product_batches
UNION ALL
SELECT '11. BATCHES WITH POSITIVE STOCK (>0)', COUNT(*)::text FROM product_batches WHERE available_quantity > 0
UNION ALL
SELECT '12. TOTAL PHYSICAL STOCK QUANTITY SUM', ROUND(SUM(available_quantity), 3)::text FROM product_batches WHERE available_quantity > 0;
"@

Write-Host "`n[SUMMARY METRICS]" -ForegroundColor Yellow
docker exec -i $Container psql -U $User -d $DbName -c "$sqlVerifications"

Write-Host "`n[CHECKING SPECIFIC ITEM: 'A VELLAM 1K']" -ForegroundColor Yellow
$sqlVellam = @"
SELECT 
    p.product_code,
    p.name,
    p.tamil_name,
    u.symbol AS uom_symbol,
    p.is_weighable,
    p.mrp,
    p.selling_price,
    b.batch_number,
    b.available_quantity AS stock_qty
FROM products p
JOIN unit_of_measures u ON p.unit_of_measure_id = u.id
LEFT JOIN product_batches b ON b.product_id = p.id
WHERE p.name LIKE '%VELLAM%' OR p.product_code = 'BA-4287'
LIMIT 5;
"@
docker exec -i $Container psql -U $User -d $DbName -c "$sqlVellam"

Write-Host "`n[CHECKING SAMPLE CUSTOMERS WITH LOYALTY POINTS]" -ForegroundColor Yellow
$sqlSampleCust = @"
SELECT 
    name,
    phone,
    membership_card_number,
    running_loyalty_points,
    running_wallet_balance
FROM customers
WHERE running_loyalty_points > 0 OR running_wallet_balance <> 0
ORDER BY running_loyalty_points DESC
LIMIT 10;
"@
docker exec -i $Container psql -U $User -d $DbName -c "$sqlSampleCust"

Write-Host "`n[DATA INTEGRITY & ANOMALY CHECKS]" -ForegroundColor Yellow
$sqlIntegrity = @"
SELECT 'Orphaned Products (Missing UOM)' AS Check_Name, COUNT(*)::text AS Anomaly_Count FROM products WHERE unit_of_measure_id IS NULL
UNION ALL
SELECT 'Orphaned Products (Missing TaxSlab)', COUNT(*)::text FROM products WHERE tax_slab_id IS NULL
UNION ALL
SELECT 'Duplicate Barcodes', COUNT(*)::text FROM (SELECT barcode_value FROM barcodes GROUP BY barcode_value HAVING COUNT(*) > 1) d
UNION ALL
SELECT 'Invalid/Blank Customer Names', COUNT(*)::text FROM customers WHERE name IS NULL OR TRIM(name) = '';
"@
docker exec -i $Container psql -U $User -d $DbName -c "$sqlIntegrity"

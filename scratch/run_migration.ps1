# Master Data Migration Script from Sigma 21 (192.168.1.10 - APPLE26-27)
param (
    [string]$Server = "192.168.1.10",
    [string]$Database = "APPLE26-27",
    [string]$Username = "sa",
    [string]$Password = "Q7!mX#92Lp@Tz4Ks",
    [string]$OutputDir = "d:\JEGADISH\APPLE_SUPERMARKET_POS_PROJECT\AI_POWERED_POS_AND_ERP_SYSTEM\scratch\migration_exports"
)

if (!(Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

$connStr = "Server=$Server;Database=$Database;User Id=$Username;Password=$Password;TrustServerCertificate=True;Connect Timeout=15;"
Write-Host "[INFO] Connecting to Sigma 21 Database ($Server -> $Database)..." -ForegroundColor Cyan

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    Write-Host "[OK] Connected successfully!" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Could not connect: $_" -ForegroundColor Red
    exit 1
}

# ==============================================================================
# 1. EXPORT CUSTOMER MASTER
# ==============================================================================
Write-Host "`n[1/5] Migrating Customer Master..." -ForegroundColor Yellow
$custSql = @"
SELECT 
    ID AS CustomerCode,
    Name,
    ISNULL(PetName, N'') AS TamilName,
    ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
    ISNULL(Email, N'') AS Email,
    ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
    ISNULL(City, N'') AS City,
    ISNULL(State, N'') AS State,
    ISNULL(PinCodeNo, N'') AS Pincode,
    ISNULL(GSTNO, N'') AS Gstin,
    ISNULL(CreditLimit, 0) AS CreditLimit,
    ISNULL(Balance, 0) AS OpeningBalance
FROM Master_Accounts
WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%'
ORDER BY ID;
"@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $custSql
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dtCust = New-Object System.Data.DataTable
$adapter.Fill($dtCust) | Out-Null

$custCsv = Join-Path $OutputDir "CustomerMaster.csv"
$dtCust | Export-Csv -Path $custCsv -NoTypeInformation -Encoding UTF8
Write-Host "[OK] Exported $($dtCust.Rows.Count) Customers to $custCsv" -ForegroundColor Green

# ==============================================================================
# 2. EXPORT SUPPLIER MASTER
# ==============================================================================
Write-Host "`n[2/5] Migrating Supplier Master..." -ForegroundColor Yellow
$suppSql = @"
SELECT 
    ID AS SupplierCode,
    Name,
    ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
    ISNULL(Email, N'') AS Email,
    ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
    ISNULL(City, N'') AS City,
    ISNULL(State, N'') AS State,
    ISNULL(PinCodeNo, N'') AS Pincode,
    ISNULL(GSTNO, N'') AS Gstin,
    N'NET30' AS PaymentTerms,
    ISNULL(Balance, 0) AS OpeningBalance
FROM Master_Accounts
WHERE FormName = 'Supplier' OR AccountType = 'Sundry Creditors' OR AccountType LIKE '%Creditor%'
ORDER BY ID;
"@

$cmd.CommandText = $suppSql
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dtSupp = New-Object System.Data.DataTable
$adapter.Fill($dtSupp) | Out-Null

$suppCsv = Join-Path $OutputDir "SupplierMaster.csv"
$dtSupp | Export-Csv -Path $suppCsv -NoTypeInformation -Encoding UTF8
Write-Host "[OK] Exported $($dtSupp.Rows.Count) Suppliers to $suppCsv" -ForegroundColor Green

# ==============================================================================
# 3. EXPORT PRODUCT MASTER (WITH PROPER UOM & BARCODE)
# ==============================================================================
Write-Host "`n[3/5] Migrating Product Master (with UOM & Barcode)..." -ForegroundColor Yellow
$prodSql = @"
SELECT 
    p.ID AS ProductCode,
    p.Name,
    ISNULL(p.TamilName, N'') AS TamilName,
    ISNULL(p.Category, N'') AS Category,
    CASE 
        WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
        WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
        ELSE 1.00 
    END AS Mrp,
    CASE 
        WHEN ISNULL(b.SalesRate1, 0) > 0 THEN CAST(b.SalesRate1 AS DECIMAL(18,2))
        WHEN ISNULL(p.Rate1, 0) > 0 THEN CAST(p.Rate1 AS DECIMAL(18,2))
        WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
        ELSE 1.00 
    END AS SellingPrice,
    CASE 
        WHEN ISNULL(b.PurchaseRate, 0) > 0 THEN CAST(b.PurchaseRate AS DECIMAL(18,2))
        WHEN ISNULL(p.PPurchaseRate, 0) > 0 THEN CAST(p.PPurchaseRate AS DECIMAL(18,2))
        ELSE 0.00 
    END AS PurchasePrice,
    CASE 
        WHEN b.BatchNo IS NOT NULL AND LEN(LTRIM(RTRIM(b.BatchNo))) >= 3 THEN LTRIM(RTRIM(b.BatchNo))
        WHEN p.ShortName IS NOT NULL AND LEN(LTRIM(RTRIM(p.ShortName))) >= 3 THEN LTRIM(RTRIM(p.ShortName))
        ELSE N'' 
    END AS Barcode,
    CASE 
        WHEN g.Percentage = 0  THEN N'GST 0%'
        WHEN g.Percentage = 5  THEN N'GST 5%'
        WHEN g.Percentage = 12 THEN N'GST 12%'
        WHEN g.Percentage = 18 THEN N'GST 18%'
        WHEN g.Percentage = 28 THEN N'GST 28%'
        ELSE N'GST 0%'
    END AS TaxSlabName,
    CASE 
        WHEN p.Weight > 0 OR p.Name LIKE '%KG%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%' THEN N'TRUE'
        ELSE N'FALSE'
    END AS IsWeighable,
    CASE 
        WHEN b.EXPDate IS NOT NULL THEN N'TRUE'
        ELSE N'FALSE'
    END AS HasExpiry,
    CASE 
        WHEN p.Weight > 0 OR p.Name LIKE '%KG%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%' THEN N'Kgs'
        WHEN p.Box = 1 THEN N'Box'
        ELSE N'Pcs'
    END AS Uom
FROM Master_Inventory_Product p
LEFT JOIN Master_Batch b ON b.ProductName = p.ID AND b.Status = 1
LEFT JOIN Master_Base_GST g ON p.GSTInterStateOutput = g.ID
WHERE p.Status = 1
ORDER BY p.ID;
"@

$cmd.CommandText = $prodSql
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dtProd = New-Object System.Data.DataTable
$adapter.Fill($dtProd) | Out-Null

$prodCsv = Join-Path $OutputDir "ProductMaster.csv"
$dtProd | Export-Csv -Path $prodCsv -NoTypeInformation -Encoding UTF8
Write-Host "[OK] Exported $($dtProd.Rows.Count) Products to $prodCsv" -ForegroundColor Green

# ==============================================================================
# 4. EXPORT STOCK & BATCH MASTER
# ==============================================================================
Write-Host "`n[4/5] Migrating Stock & Batch Master..." -ForegroundColor Yellow
$stockSql = @"
SELECT 
    b.ID AS BatchId,
    b.ProductName AS ProductCode,
    p.Name AS ProductName,
    ISNULL(b.BatchNo, N'DEFAULT') AS BatchNumber,
    b.EXPDate AS ExpiryDate,
    CAST(ISNULL(b.Stock, 0) AS DECIMAL(18,3)) AS CurrentStock,
    CAST(ISNULL(b.MRP, 0) AS DECIMAL(18,2)) AS Mrp,
    CAST(ISNULL(b.SalesRate1, 0) AS DECIMAL(18,2)) AS SellingPrice,
    CAST(ISNULL(b.PurchaseRate, 0) AS DECIMAL(18,2)) AS PurchasePrice
FROM Master_Batch b
INNER JOIN Master_Inventory_Product p ON b.ProductName = p.ID
WHERE b.Status = 1 AND b.Stock > 0
ORDER BY b.ProductName;
"@

$cmd.CommandText = $stockSql
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dtStock = New-Object System.Data.DataTable
$adapter.Fill($dtStock) | Out-Null

$stockCsv = Join-Path $OutputDir "StockMaster.csv"
$dtStock | Export-Csv -Path $stockCsv -NoTypeInformation -Encoding UTF8
Write-Host "[OK] Exported $($dtStock.Rows.Count) Active Stock Batches to $stockCsv" -ForegroundColor Green

# ==============================================================================
# 5. EXPORT CUSTOMER LEDGER TRANSACTIONS
# ==============================================================================
Write-Host "`n[5/5] Migrating Customer Ledger Transactions..." -ForegroundColor Yellow
$ledgerSql = @"
SELECT TOP 5000
    t.VNo AS ReferenceNumber,
    t.Date AS EntryDate,
    a.ID AS CustomerCode,
    a.Name AS CustomerName,
    t.Type AS TransactionType,
    CAST(ISNULL(t.Gross, 0) AS DECIMAL(18,2)) AS GrossAmount,
    CAST(ISNULL(t.Discount, 0) AS DECIMAL(18,2)) AS DiscountAmount,
    CAST(ISNULL(t.GSTGross, 0) AS DECIMAL(18,2)) AS GSTTaxAmount,
    CAST(ISNULL(t.NetAmount, 0) AS DECIMAL(18,2)) AS NetAmount,
    ISNULL(t.Remarks1, N'') AS Narration
FROM Trans_Accounts t
INNER JOIN Master_Accounts a ON t.Account = a.ID OR t.ContraAccount = a.ID
WHERE a.FormName = 'Customer' OR a.AccountType = 'Sundry Debtors' OR a.AccountType LIKE '%Debtor%'
ORDER BY t.Date DESC;
"@

$cmd.CommandText = $ledgerSql
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dtLedger = New-Object System.Data.DataTable
$adapter.Fill($dtLedger) | Out-Null

$ledgerCsv = Join-Path $OutputDir "CustomerLedger.csv"
$dtLedger | Export-Csv -Path $ledgerCsv -NoTypeInformation -Encoding UTF8
Write-Host "[OK] Exported $($dtLedger.Rows.Count) Customer Ledger Rows to $ledgerCsv" -ForegroundColor Green

$conn.Close()

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host " MASTER DATA MIGRATION EXPORT COMPLETED!" -ForegroundColor Green
Write-Host " All CSV files saved to: $OutputDir" -ForegroundColor Yellow
Write-Host "==================================================" -ForegroundColor Green

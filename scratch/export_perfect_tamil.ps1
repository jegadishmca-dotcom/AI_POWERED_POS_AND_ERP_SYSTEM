# ==============================================================================
# Export Product Master from SQL Server (192.168.1.10) with 100% Unicode Support
# Run this in PowerShell on 192.168.1.10 (or via Remote Desktop)
# ==============================================================================

$serverName = ".\SQLEXPRESS"  # Adjust if server instance name is different
$databaseName = "APPLE26-27"
$outputFile = "C:\product_master_PERFECT_TAMIL.csv"

Write-Host "[INFO] Connecting to SQL Server database $databaseName on $serverName..." -ForegroundColor Cyan

$connStr = "Server=$serverName;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $conn.Open()
    Write-Host "[OK] Connected successfully!" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "[INFO] Trying default instance 'localhost'..." -ForegroundColor Yellow
    $connStr = "Server=localhost;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
}

$sql = @"
WITH LatestBatch AS (
    SELECT 
        ProductName,
        MRP,
        PurchaseRate,
        SalesRate1,
        ROW_NUMBER() OVER (PARTITION BY ProductName ORDER BY AID DESC) AS rn
    FROM Master_Batch
    WHERE Status = 1
),
ProductData AS (
    SELECT 
        p.ID,
        p.Name,
        ISNULL(p.TamilName, N'') AS TamilName,
        ISNULL(p.Category, N'') AS Category,
        p.ShortName,
        p.GSTInterStateOutput,
        p.AltUnit1Unit,
        CASE 
            WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
            WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
            ELSE 1.00 
        END AS FinalMrp,
        CASE 
            WHEN ISNULL(b.SalesRate1, 0) > 0 THEN CAST(b.SalesRate1 AS DECIMAL(18,2))
            WHEN ISNULL(p.Rate1, 0) > 0 THEN CAST(p.Rate1 AS DECIMAL(18,2))
            WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
            WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
            ELSE 1.00 
        END AS RawSellingPrice,
        CASE 
            WHEN ISNULL(b.PurchaseRate, 0) > 0 THEN CAST(b.PurchaseRate AS DECIMAL(18,2))
            WHEN ISNULL(p.PPurchaseRate, 0) > 0 THEN CAST(p.PPurchaseRate AS DECIMAL(18,2))
            ELSE 0 
        END AS RawPurchasePrice
    FROM Master_Inventory_Product p
    LEFT JOIN LatestBatch b ON b.ProductName = p.ID AND b.rn = 1
    WHERE p.Status = 1
)
SELECT 
    pd.ID AS ProductCode,
    pd.Name,
    pd.TamilName,
    pd.Category AS Description,
    pd.FinalMrp AS Mrp,
    CASE WHEN pd.RawSellingPrice > pd.FinalMrp THEN pd.FinalMrp ELSE pd.RawSellingPrice END AS SellingPrice,
    CASE WHEN pd.RawPurchasePrice > 0 THEN pd.RawPurchasePrice ELSE CAST(pd.RawSellingPrice * 0.80 AS DECIMAL(18,2)) END AS PurchasePrice,
    CASE WHEN pd.ShortName IS NOT NULL AND LEN(pd.ShortName) >= 8 AND ISNUMERIC(pd.ShortName) = 1 THEN pd.ShortName ELSE N'' END AS Barcode,
    CASE 
        WHEN g.Percentage = 0  THEN N'GST 0%'
        WHEN g.Percentage = 5  THEN N'GST 5%'
        WHEN g.Percentage = 12 THEN N'GST 12%'
        WHEN g.Percentage = 18 THEN N'GST 18%'
        WHEN g.Percentage = 28 THEN N'GST 28%'
        ELSE N'GST 0%'
    END AS TaxSlabName,
    N'FALSE' AS IsWeighable,
    N'FALSE' AS HasExpiry,
    N'Pcs' AS Uom
FROM ProductData pd
LEFT JOIN Master_Base_GST g ON pd.GSTInterStateOutput = g.ID
ORDER BY pd.ID;
"@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.CommandTimeout = 300
$reader = $cmd.ExecuteReader()

$csvLines = [System.Collections.Generic.List[string]]::new()
$csvLines.Add("ProductCode,Name,TamilName,Description,Mrp,SellingPrice,PurchasePrice,Barcode,TaxSlabName,IsWeighable,HasExpiry,Uom")

$count = 0
$tamilCount = 0

while ($reader.Read()) {
    $count++
    $code = $reader["ProductCode"].ToString()
    $name = $reader["Name"].ToString() -replace '"', '""'
    $tamil = $reader["TamilName"].ToString() -replace '"', '""'
    $desc = $reader["Description"].ToString() -replace '"', '""'
    $mrp = $reader["Mrp"]
    $selling = $reader["SellingPrice"]
    $purchase = $reader["PurchasePrice"]
    $barcode = $reader["Barcode"].ToString()
    $tax = $reader["TaxSlabName"].ToString()
    $weigh = $reader["IsWeighable"].ToString()
    $expiry = $reader["HasExpiry"].ToString()
    $uom = $reader["Uom"].ToString()

    if ($tamil -match '[\u0B80-\u0BFF]') { $tamilCount++ }

    $line = "$code,`"$name`",`"$tamil`",`"$desc`",$mrp,$selling,$purchase,$barcode,$tax,$weigh,$expiry,$uom"
    $csvLines.Add($line)
}

$reader.Close()
$conn.Close()

# Save with UTF-8 BOM
$utf8Bom = [System.Text.UTF8Encoding]::new($true)
[System.IO.File]::WriteAllLines($outputFile, $csvLines.ToArray(), $utf8Bom)

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " EXPORT COMPLETE!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Total products exported: $count" -ForegroundColor Cyan
Write-Host " Total products with Tamil characters: $tamilCount" -ForegroundColor Green
Write-Host " File saved to: $outputFile" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green

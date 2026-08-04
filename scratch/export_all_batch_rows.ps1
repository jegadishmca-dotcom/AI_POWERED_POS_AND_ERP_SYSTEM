# ==============================================================================
# Export Product Master from SQL Server (192.168.1.10) - Multi-Batch Complete Export
# Run this script in PowerShell on 192.168.1.10 (SQL Server machine)
# ==============================================================================

$serverName = ".\SQLEXPRESS"   # Adjust if your SQL Server instance name is different
$databaseName = "APPLE26-27"
$outputFile = "C:\product_master_ALL_BATCHES.csv"

Write-Host "[INFO] Connecting to SQL Server database $databaseName..." -ForegroundColor Cyan

$connStr = "Server=$serverName;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $conn.Open()
    Write-Host "[OK] Connected successfully to SQL Server!" -ForegroundColor Green
} catch {
    Write-Host "[INFO] Retrying with localhost..." -ForegroundColor Yellow
    $connStr = "Server=localhost;Database=$databaseName;Integrated Security=True;TrustServerCertificate=True"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
}

# SQL Query: Includes EVERY active batch row from Master_Batch so multi-batch items export all batch rates
$sql = @"
SELECT 
    p.ID AS ProductCode,
    p.Name,
    ISNULL(p.TamilName, N'') AS TamilName,
    ISNULL(p.Category, N'') AS Description,
    CASE 
        WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
        WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
        ELSE 1.00 
    END AS Mrp,
    CASE 
        WHEN ISNULL(b.SalesRate1, 0) > 0 THEN CAST(b.SalesRate1 AS DECIMAL(18,2))
        WHEN ISNULL(p.Rate1, 0) > 0 THEN CAST(p.Rate1 AS DECIMAL(18,2))
        WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
        WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
        ELSE 1.00 
    END AS SellingPrice,
    CASE 
        WHEN ISNULL(b.PurchaseRate, 0) > 0 THEN CAST(b.PurchaseRate AS DECIMAL(18,2))
        WHEN ISNULL(p.PPurchaseRate, 0) > 0 THEN CAST(p.PPurchaseRate AS DECIMAL(18,2))
        ELSE 0 
    END AS PurchasePrice,
    -- Barcode selection: Priority 1: Manufacturer Barcode (ShortName), Priority 2: BatchNo
    CASE 
        WHEN b.BatchNo IS NOT NULL AND LEN(LTRIM(RTRIM(b.BatchNo))) >= 3 
            THEN LTRIM(RTRIM(b.BatchNo))
        WHEN p.ShortName IS NOT NULL AND LEN(LTRIM(RTRIM(p.ShortName))) >= 3 
            THEN LTRIM(RTRIM(p.ShortName))
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
    N'FALSE' AS IsWeighable,
    N'FALSE' AS HasExpiry,
    N'Pcs' AS Uom
FROM Master_Inventory_Product p
LEFT JOIN Master_Batch b ON b.ProductName = p.ID AND b.Status = 1
LEFT JOIN Master_Base_GST g ON p.GSTInterStateOutput = g.ID
WHERE p.Status = 1
ORDER BY p.ID;
"@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.CommandTimeout = 300
$reader = $cmd.ExecuteReader()

$csvLines = [System.Collections.Generic.List[string]]::new()
$csvLines.Add("ProductCode,Name,TamilName,Description,Mrp,SellingPrice,PurchasePrice,Barcode,TaxSlabName,IsWeighable,HasExpiry,Uom")

$count = 0; $barcodeCount = 0; $tamilCount = 0
while ($reader.Read()) {
    $count++
    $code = $reader["ProductCode"].ToString()
    $name = $reader["Name"].ToString() -replace '"', '""'
    $tamil = $reader["TamilName"].ToString() -replace '"', '""'
    $desc = $reader["Description"].ToString() -replace '"', '""'
    $mrp = $reader["Mrp"]; $selling = $reader["SellingPrice"]; $purchase = $reader["PurchasePrice"]
    $barcode = $reader["Barcode"].ToString().Trim()
    $tax = $reader["TaxSlabName"].ToString()
    $weigh = $reader["IsWeighable"].ToString(); $expiry = $reader["HasExpiry"].ToString(); $uom = $reader["Uom"].ToString()

    if ($barcode.Length -ge 3) { $barcodeCount++ }
    if ($tamil -match '[\u0B80-\u0BFF]') { $tamilCount++ }

    $csvLines.Add("$code,`"$name`",`"$tamil`",`"$desc`",$mrp,$selling,$purchase,$barcode,$tax,$weigh,$expiry,$uom")
}
$reader.Close(); $conn.Close()

[System.IO.File]::WriteAllLines($outputFile, $csvLines.ToArray(), [System.Text.UTF8Encoding]::new($true))
Write-Host "MULTI-BATCH EXPORT COMPLETE! Total Exported Rows: $count | Total Barcodes/Batches: $barcodeCount | Tamil: $tamilCount. Saved to $outputFile" -ForegroundColor Green

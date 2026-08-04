# Analyze Stock Master & Current Batch Stock Levels in Sigma 21
param (
    [string]$Server = "192.168.1.10",
    [string]$Database = "APPLE26-27",
    [string]$Username = "sa",
    [string]$Password = "Q7!mX#92Lp@Tz4Ks"
)

$connStr = "Server=$Server;Database=$Database;User Id=$Username;Password=$Password;TrustServerCertificate=True;Connect Timeout=15;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $conn.Open()
    Write-Host "[INFO] Querying Stock Master in Sigma 21 ($Database)..." -ForegroundColor Cyan

    # 1. Total Stock Count & Quantity in Master_Batch
    $sqlBatchStats = @"
    SELECT 
        COUNT(*) AS TotalBatchRecords,
        COUNT(CASE WHEN Stock > 0 THEN 1 END) AS BatchesWithPositiveStock,
        SUM(CASE WHEN Stock > 0 THEN CAST(Stock AS DECIMAL(18,3)) ELSE 0 END) AS TotalPositiveStockQty,
        COUNT(DISTINCT ProductName) AS ProductsWithStock
    FROM Master_Batch
    WHERE Status = 1;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sqlBatchStats
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dtStats = New-Object System.Data.DataTable
    $adapter.Fill($dtStats) | Out-Null

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " SIGMA 21 STOCK MASTER BATCH STATS" -ForegroundColor Green
    Write-Host " Total Active Batch Records: $($dtStats.Rows[0]['TotalBatchRecords'])" -ForegroundColor Yellow
    Write-Host " Batches with Positive Stock (>0): $($dtStats.Rows[0]['BatchesWithPositiveStock'])" -ForegroundColor Cyan
    Write-Host " Unique Products with Stock: $($dtStats.Rows[0]['ProductsWithStock'])" -ForegroundColor Magenta
    Write-Host " Total Stock Quantity Sum: $($dtStats.Rows[0]['TotalPositiveStockQty'])" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green

    # Sample Stock Batches
    $sqlSample = @"
    SELECT TOP 20
        b.ID AS BatchId,
        b.ProductName AS ProductCode,
        p.Name AS ProductName,
        ISNULL(b.BatchNo, N'DEFAULT') AS BatchNumber,
        CAST(ISNULL(b.Stock, 0) AS DECIMAL(18,3)) AS CurrentStock,
        CAST(ISNULL(b.MRP, 0) AS DECIMAL(18,2)) AS Mrp,
        CAST(ISNULL(b.SalesRate1, 0) AS DECIMAL(18,2)) AS SellingPrice,
        CAST(ISNULL(b.PurchaseRate, 0) AS DECIMAL(18,2)) AS PurchasePrice,
        b.EXPDate AS ExpiryDate
    FROM Master_Batch b
    INNER JOIN Master_Inventory_Product p ON b.ProductName = p.ID
    WHERE b.Status = 1 AND b.Stock > 0
    ORDER BY b.Stock DESC;
"@

    $cmd.CommandText = $sqlSample
    $dtSample = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtSample) | Out-Null

    Write-Host "`nTop 20 Stock Batches in Sigma 21 (by Current Stock):" -ForegroundColor Yellow
    $dtSample | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

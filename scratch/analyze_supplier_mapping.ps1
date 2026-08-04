# Analyze Supplier-Product mappings in Sigma 21
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

    Write-Host "`n=== 1. Checking SupplierName in Master_Batch ===" -ForegroundColor Yellow
    $sql1 = @"
    SELECT TOP 10 
        b.ProductName AS ProductCode,
        p.Name AS ProductName,
        b.SupplierName,
        a.ID AS SupplierCode,
        a.Name AS SupplierAccountName
    FROM Master_Batch b
    LEFT JOIN Master_Inventory_Product p ON b.ProductName = p.ID
    LEFT JOIN Master_Accounts a ON b.SupplierName = a.ID OR b.SupplierName = a.Name
    WHERE b.SupplierName IS NOT NULL AND b.SupplierName <> ''
"@
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql1
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt1 = New-Object System.Data.DataTable
    $adapter.Fill($dt1) | Out-Null
    $dt1 | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    Write-Host "`n=== 2. Checking Distinct Non-Empty SupplierName Count in Master_Batch ===" -ForegroundColor Yellow
    $sql2 = "SELECT COUNT(DISTINCT ProductName) AS ProductsWithBatchSupplier FROM Master_Batch WHERE SupplierName IS NOT NULL AND SupplierName <> '';"
    $cmd.CommandText = $sql2
    $count1 = $cmd.ExecuteScalar()
    Write-Host "Products with Supplier in Master_Batch: $count1" -ForegroundColor Green

    Write-Host "`n=== 3. Checking Company in Master_Inventory_Product ===" -ForegroundColor Yellow
    $sql3 = "SELECT COUNT(DISTINCT ID) AS ProductsWithCompany FROM Master_Inventory_Product WHERE Company IS NOT NULL AND Company <> '';"
    $cmd.CommandText = $sql3
    $count2 = $cmd.ExecuteScalar()
    Write-Host "Products with Company in Master_Inventory_Product: $count2" -ForegroundColor Green

    Write-Host "`n=== 4. Checking Purchase Transactions in Trans_Inventory_SOM ===" -ForegroundColor Yellow
    $sql4 = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Trans_Inventory_SOM';"
    $cmd.CommandText = $sql4
    $reader = $cmd.ExecuteReader()
    $somCols = @()
    while ($reader.Read()) { $somCols += $reader["COLUMN_NAME"] }
    $reader.Close()
    Write-Host "Trans_Inventory_SOM Columns: $($somCols -join ', ')" -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

# Inspect Sigma 21 Database for Supplier-to-Product Relationships
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
    Write-Host "[OK] Connected to Sigma 21!" -ForegroundColor Green

    # 1. Search for foreign keys / columns in Master_Inventory_Product & Master_Batch referencing accounts/suppliers
    Write-Host "`n--- Checking Columns in Master_Inventory_Product ---" -ForegroundColor Yellow
    $prodColsSql = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Master_Inventory_Product';"
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $prodColsSql
    $reader = $cmd.ExecuteReader()
    $cols = @()
    while ($reader.Read()) { $cols += $reader["COLUMN_NAME"] }
    $reader.Close()
    Write-Host ($cols -join ", ") -ForegroundColor Cyan

    Write-Host "`n--- Checking Columns in Master_Batch ---" -ForegroundColor Yellow
    $batchColsSql = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Master_Batch';"
    $cmd.CommandText = $batchColsSql
    $reader = $cmd.ExecuteReader()
    $bCols = @()
    while ($reader.Read()) { $bCols += $reader["COLUMN_NAME"] }
    $reader.Close()
    Write-Host ($bCols -join ", ") -ForegroundColor Cyan

    # 2. Check Purchase transaction tables linking Supplier (Master_Accounts) to Product
    Write-Host "`n--- Searching for Purchase Transaction Tables ---" -ForegroundColor Yellow
    $tablesSql = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%Pur%' OR TABLE_NAME LIKE '%GRN%' OR TABLE_NAME LIKE '%Trans%' ORDER BY TABLE_NAME;"
    $cmd.CommandText = $tablesSql
    $reader = $cmd.ExecuteReader()
    $tList = @()
    while ($reader.Read()) { $tList += $reader["TABLE_NAME"] }
    $reader.Close()
    Write-Host ($tList -join ", ") -ForegroundColor Green

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

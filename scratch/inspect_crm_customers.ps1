# Inspect Master_CRM_PointsCustomer and CustomerBalance in Sigma 21
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
    Write-Host "[INFO] Inspecting Master_CRM_PointsCustomer..." -ForegroundColor Cyan

    $sqlCols = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Master_CRM_PointsCustomer'"
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sqlCols
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dtCols = New-Object System.Data.DataTable
    $adapter.Fill($dtCols) | Out-Null

    Write-Host "`nMaster_CRM_PointsCustomer Columns:" -ForegroundColor Yellow
    $dtCols | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    # Top 20 records
    $sqlSample = "SELECT TOP 20 * FROM Master_CRM_PointsCustomer ORDER BY ID DESC"
    $cmd.CommandText = $sqlSample
    $dtSample = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtSample) | Out-Null

    Write-Host "`nTop 20 CRM Customers in Sigma 21:" -ForegroundColor Yellow
    $dtSample | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Green

    # Inspect CustomerBalance columns and top records
    Write-Host "`nInspecting CustomerBalance table..." -ForegroundColor Cyan
    $sqlBalCols = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CustomerBalance'"
    $cmd.CommandText = $sqlBalCols
    $dtBalCols = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtBalCols) | Out-Null

    Write-Host "`nCustomerBalance Columns:" -ForegroundColor Yellow
    $dtBalCols | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $sqlBalSample = "SELECT TOP 20 * FROM CustomerBalance ORDER BY ID DESC"
    $cmd.CommandText = $sqlBalSample
    $dtBalSample = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtBalSample) | Out-Null

    Write-Host "`nTop 20 Customer Balances in Sigma 21:" -ForegroundColor Yellow
    $dtBalSample | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Green

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

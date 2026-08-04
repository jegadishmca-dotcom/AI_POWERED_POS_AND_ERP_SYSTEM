# Inspect SupplierName in Master_Batch
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

    $sql = @"
    SELECT TOP 30 
        b.SupplierName,
        COUNT(*) AS ProductCount
    FROM Master_Batch b
    WHERE b.SupplierName IS NOT NULL AND b.SupplierName <> ''
    GROUP BY b.SupplierName
    ORDER BY COUNT(*) DESC;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    $dt | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

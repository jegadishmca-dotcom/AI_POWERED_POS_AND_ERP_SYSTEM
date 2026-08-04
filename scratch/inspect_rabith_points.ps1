# Query Trans_CRM_PointsLedger for Rabith (ASM1318) in Sigma 21

$sqlConnStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($sqlConnStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 30 * FROM Trans_CRM_PointsLedger WHERE CustomerID = 'ASM1318' OR CustomerName LIKE '%Rabith%' ORDER BY ID DESC;"
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    $dt | Format-Table -AutoSize
    $conn.Close()
} catch {
    Write-Host "Error: $_"
}

# Inspect Rabith's data in Sigma 21 and PostgreSQL posdb_uat

$sqlConnStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;"

Write-Host "=== SIGMA 21: Master_CRM_PointsCustomer for Rabith / 9385729616 ==="
$query1 = @"
SELECT ID, CustomerID, Name, Mobile1, Mobile2, Phone1, BalancePoint, Balance, TotalPurchase 
FROM Master_CRM_PointsCustomer 
WHERE Mobile1 LIKE '%9385729616%' OR Mobile2 LIKE '%9385729616%' OR Phone1 LIKE '%9385729616%' OR Name LIKE '%Rabith%';
"@

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($sqlConnStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $query1
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    $dt | Format-Table -AutoSize
    
    $custCode = if ($dt.Rows.Count -gt 0) { $dt.Rows[0]["ID"] } else { "ASM1318" }

    Write-Host "`n=== SIGMA 21: Trans_CRM_PointsLedger / Points history for $custCode ==="
    $query2 = @"
    SELECT TOP 30 * FROM sys.tables WHERE name LIKE '%Points%' OR name LIKE '%CRM%';
"@
    $cmd2 = $conn.CreateCommand()
    $cmd2.CommandText = $query2
    $adapter2 = New-Object System.Data.SqlClient.SqlDataAdapter($cmd2)
    $dt2 = New-Object System.Data.DataTable
    $adapter2.Fill($dt2) | Out-Null
    $dt2 | Format-Table -AutoSize

    $conn.Close()
} catch {
    Write-Host "Error connecting to Sigma 21: $_"
}

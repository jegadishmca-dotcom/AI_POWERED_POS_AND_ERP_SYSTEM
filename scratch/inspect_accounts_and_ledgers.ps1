# Sample Accounts and Ledger schema
$connStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;Connect Timeout=10;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

Write-Host "=== Master_Accounts_Type ===" -ForegroundColor Yellow
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT ID, Name FROM Master_Accounts_Type;"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  ID: $($reader['ID']) | Name: $($reader['Name'])"
}
$reader.Close()

Write-Host "`n=== Master_Accounts Summary ===" -ForegroundColor Yellow
$cmd.CommandText = "SELECT AccountGroup, COUNT(*) as Count FROM Master_Accounts GROUP BY AccountGroup;"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  Group ID: $($reader['AccountGroup']) | Count: $($reader['Count'])"
}
$reader.Close()

Write-Host "`n=== Sample Customers (Top 5) ===" -ForegroundColor Yellow
$cmd.CommandText = "SELECT TOP 5 ID, Name, Phone, Address1, GSTIN, OpeningBalance, ClosingBalance FROM Master_Accounts WHERE AccountGroup = 2 OR AccountGroup = 'AG-2' OR Name LIKE '%Customer%';"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
foreach ($col in $dt.Columns) { Write-Host "$($col.ColumnName)`t" -NoNewline }
Write-Host ""
foreach ($row in $dt.Rows) {
    Write-Host "$($row['ID'])`t$($row['Name'])`t$($row['Phone'])`t$($row['OpeningBalance'])`t$($row['ClosingBalance'])"
}

$conn.Close()

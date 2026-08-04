# Search all tables in APPLE26-27 for Customer data
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

    $sqlTables = @"
    SELECT TABLE_NAME 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME LIKE '%CUST%' OR TABLE_NAME LIKE '%MEMBER%' OR TABLE_NAME LIKE '%DEBTOR%' OR TABLE_NAME LIKE '%CARD%' OR TABLE_NAME LIKE '%CLIENT%'
    ORDER BY TABLE_NAME;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sqlTables
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dtTables = New-Object System.Data.DataTable
    $adapter.Fill($dtTables) | Out-Null

    Write-Host "`nTables containing Customer/Member/Card/Debtor in Name:" -ForegroundColor Yellow
    $dtTables | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    foreach ($row in $dtTables.Rows) {
        $tName = $row["TABLE_NAME"]
        try {
            $cmdCount = $conn.CreateCommand()
            $cmdCount.CommandText = "SELECT COUNT(*) FROM [$tName]"
            $cnt = $cmdCount.ExecuteScalar()
            Write-Host "Table [$tName]: $cnt records" -ForegroundColor Green
        } catch {
            Write-Host "Table [$tName]: Error querying" -ForegroundColor Red
        }
    }

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

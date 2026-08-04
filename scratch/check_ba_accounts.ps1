# Check Master_Accounts for BA-xxx codes
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
    SELECT ID, Name, FormName, AccountType 
    FROM Master_Accounts 
    WHERE ID IN ('BA-423', 'BA-753', 'BA-733', 'BA-729', 'BA-745', 'BA-748', 'BA-854', 'BA-761', 'BA-862', 'BA-796');
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

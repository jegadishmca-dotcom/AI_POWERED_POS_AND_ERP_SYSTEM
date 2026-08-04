# Sample schemas and row counts for Master tables
$connStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;Connect Timeout=10;"

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

function SampleTable($tableName, $top = 5) {
    Write-Host "`n==================================================" -ForegroundColor Cyan
    Write-Host " TABLE: $tableName" -ForegroundColor Yellow
    Write-Host "==================================================" -ForegroundColor Cyan
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$tableName];"
    $count = $cmd.ExecuteScalar()
    Write-Host "Total Row Count: $count" -ForegroundColor Green

    $cmd.CommandText = "SELECT TOP $top * FROM [$tableName];"
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null

    Write-Host "Columns: $($dt.Columns.ColumnName -join ', ')" -ForegroundColor Gray
    
    foreach ($row in $dt.Rows) {
        $line = @()
        foreach ($col in $dt.Columns) {
            $val = $row[$col.ColumnName]
            if ($val -eq [DBNull]::Value) { $val = "NULL" }
            $line += "$($col.ColumnName): $val"
        }
        Write-Host "  -> $($line -join ' | ')" -ForegroundColor White
    }
}

SampleTable "Master_Accounts" 3
SampleTable "Master_Accounts_Type" 10
SampleTable "Master_Inventory_Product" 3
SampleTable "Master_Inventory_Unit" 10
SampleTable "Master_Batch" 3

$conn.Close()

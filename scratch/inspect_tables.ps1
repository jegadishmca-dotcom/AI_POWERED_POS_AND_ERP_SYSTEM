# Test connection and inspect tables on SQL Server 192.168.1.10
param (
    [string]$Server = "192.168.1.10",
    [string]$Database = "APPLE26-27",
    [string]$Username = "sa",
    [string]$Password = "Q7!mX#92Lp@Tz4Ks"
)

$connStr = "Server=$Server;Database=$Database;User Id=$Username;Password=$Password;TrustServerCertificate=True;Connect Timeout=10;"

Write-Host "Connecting to SQL Server ($Server)..." -ForegroundColor Cyan

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    Write-Host "[OK] Connected successfully to SQL Server ($Server) Database: $Database!" -ForegroundColor Green

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;"
    $reader = $cmd.ExecuteReader()
    
    Write-Host "`nTables in database '$Database':" -ForegroundColor Yellow
    $tables = [System.Collections.Generic.List[string]]::new()
    while ($reader.Read()) {
        $t = $reader['TABLE_NAME'].ToString()
        $tables.Add($t)
        Write-Host " - $t"
    }
    $reader.Close()

    Write-Host "`nTotal Tables Found: $($tables.Count)" -ForegroundColor Green

    # Filter tables for Customer, Supplier, Product, Batch, Stock, Ledger
    $relevant = $tables | Where-Object { $_ -match "Customer|Supplier|Vendor|Product|Batch|Stock|Ledger|Item|Master|Unit|Barcode|Price|Rate|Tax|GST" }
    Write-Host "`nRelevant Master Data Tables:" -ForegroundColor Cyan
    foreach ($r in $relevant) {
        Write-Host " [RELEVANT] $r"
    }

    $conn.Close()
} catch {
    Write-Host "[ERROR] Connection failed: $_" -ForegroundColor Red
}

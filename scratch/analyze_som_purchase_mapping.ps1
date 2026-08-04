# Query Trans_Inventory_SOM Purchase Bills for Product-to-Supplier Mapping
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
    SELECT 
        COUNT(DISTINCT ProductName) AS ProductsInPurchaseBills,
        COUNT(DISTINCT Account) AS DistinctSuppliersInBills
    FROM Trans_Inventory_SOM 
    WHERE FormName = 'Purchase' AND Account IS NOT NULL AND Account <> '';
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    $dt | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    Write-Host "`nSample Purchase Bill Mappings:" -ForegroundColor Yellow
    $sampleSql = @"
    SELECT TOP 15
        t.ProductName AS ProductCode,
        p.Name AS ProductName,
        t.Account AS SupplierCode,
        a.Name AS SupplierName,
        MAX(t.Date) AS LastPurchaseDate
    FROM Trans_Inventory_SOM t
    LEFT JOIN Master_Inventory_Product p ON t.ProductName = p.ID
    LEFT JOIN Master_Accounts a ON t.Account = a.ID
    WHERE t.FormName = 'Purchase' AND t.Account IS NOT NULL AND t.Account <> ''
    GROUP BY t.ProductName, p.Name, t.Account, a.Name
    ORDER BY MAX(t.Date) DESC;
"@
    $cmd.CommandText = $sampleSql
    $dtSample = New-Object System.Data.DataTable
    $adapter.Fill($dtSample) | Out-Null
    $dtSample | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Green

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

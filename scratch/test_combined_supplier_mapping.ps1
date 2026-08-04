# Combined Supplier-Product Mapping Resolution Test
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
    WITH RecentPurchase AS (
        -- Priority 1: Get the supplier from the latest Purchase Bill in Trans_Inventory_SOM
        SELECT 
            ProductName AS ProductCode,
            Account AS SupplierCode,
            ROW_NUMBER() OVER (PARTITION BY ProductName ORDER BY Date DESC, VNO DESC) AS rnk
        FROM Trans_Inventory_SOM
        WHERE FormName = 'Purchase' 
          AND Account IS NOT NULL AND Account <> ''
          AND Account IN (SELECT ID FROM Master_Accounts)
    ),
    BatchSupplier AS (
        -- Priority 2: Get supplier code from Master_Batch
        SELECT 
            b.ProductName AS ProductCode,
            b.SupplierName AS SupplierCode,
            ROW_NUMBER() OVER (PARTITION BY b.ProductName ORDER BY b.ID DESC) AS rnk
        FROM Master_Batch b
        WHERE b.SupplierName IS NOT NULL AND b.SupplierName <> ''
          AND b.SupplierName IN (SELECT ID FROM Master_Accounts)
    )
    SELECT 
        p.ID AS ProductCode,
        p.Name AS ProductName,
        COALESCE(rp.SupplierCode, bs.SupplierCode) AS MappedSupplierCode,
        supp.Name AS MappedSupplierName
    FROM Master_Inventory_Product p
    LEFT JOIN RecentPurchase rp ON p.ID = rp.ProductCode AND rp.rnk = 1
    LEFT JOIN BatchSupplier bs ON p.ID = bs.ProductCode AND bs.rnk = 1
    LEFT JOIN Master_Accounts supp ON COALESCE(rp.SupplierCode, bs.SupplierCode) = supp.ID
    WHERE p.Status = 1;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 300
    $cmd.CommandText = $sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null

    $totalProducts = $dt.Rows.Count
    $mappedRows = $dt.Select("MappedSupplierCode IS NOT NULL AND MappedSupplierCode <> ''")
    $mappedCount = $mappedRows.Count

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " COMBINED SUPPLIER MAPPING SCAN COMPLETE" -ForegroundColor Green
    Write-Host " Total Active Products: $totalProducts" -ForegroundColor Yellow
    Write-Host " Successfully Mapped to Suppliers: $mappedCount ($([math]::Round(($mappedCount/$totalProducts)*100, 2))%)" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green

    Write-Host "`nSample Mapped Products:" -ForegroundColor Yellow
    $dt | Where-Object { $_.MappedSupplierName -ne $null -and $_.MappedSupplierName -ne '' } | Select-Object -First 20 ProductCode, ProductName, MappedSupplierCode, MappedSupplierName | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

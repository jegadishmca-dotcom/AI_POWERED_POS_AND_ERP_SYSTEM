# Extract and Test Supplier-Product Mapping from Sigma 21
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
    Write-Host "[INFO] Testing Supplier Mapping Resolution from Sigma 21..." -ForegroundColor Cyan

    $sql = @"
    WITH LastPurchase AS (
        -- Get the most recent Purchase transaction supplier for each product
        SELECT 
            ProductName AS ProductCode,
            Account AS SupplierCode,
            ROW_NUMBER() OVER (PARTITION BY ProductName ORDER BY Date DESC, VNO DESC) AS rnk
        FROM Trans_Inventory_SOM
        WHERE (FormName LIKE '%Purchase%' OR Type LIKE '%Purchase%' OR Type LIKE '%GRN%')
          AND Account IN (SELECT ID FROM Master_Accounts WHERE FormName = 'Supplier' OR AccountType = 'Sundry Creditors' OR AccountType LIKE '%Creditor%')
    ),
    BatchSupplier AS (
        -- Fallback: Get supplier from Master_Batch
        SELECT 
            b.ProductName AS ProductCode,
            b.SupplierName AS SupplierCode,
            ROW_NUMBER() OVER (PARTITION BY b.ProductName ORDER BY b.ID DESC) AS rnk
        FROM Master_Batch b
        WHERE b.SupplierName IN (SELECT ID FROM Master_Accounts WHERE FormName = 'Supplier' OR AccountType = 'Sundry Creditors' OR AccountType LIKE '%Creditor%')
    )
    SELECT 
        p.ID AS ProductCode,
        p.Name AS ProductName,
        COALESCE(lp.SupplierCode, bs.SupplierCode) AS MappedSupplierCode,
        supp.Name AS MappedSupplierName
    FROM Master_Inventory_Product p
    LEFT JOIN LastPurchase lp ON p.ID = lp.ProductCode AND lp.rnk = 1
    LEFT JOIN BatchSupplier bs ON p.ID = bs.ProductCode AND bs.rnk = 1
    LEFT JOIN Master_Accounts supp ON COALESCE(lp.SupplierCode, bs.SupplierCode) = supp.ID
    WHERE p.Status = 1;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 300
    $cmd.CommandText = $sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null

    $totalProducts = $dt.Rows.Count
    $mappedCount = ($dt.Select("MappedSupplierCode IS NOT NULL AND MappedSupplierCode <> ''")).Count

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " SUPPLIER MAPPING SCAN COMPLETE" -ForegroundColor Green
    Write-Host " Total Active Products in Sigma 21: $totalProducts" -ForegroundColor Yellow
    Write-Host " Successfully Mapped to Suppliers: $mappedCount ($([math]::Round(($mappedCount/$totalProducts)*100, 2))%)" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green

    Write-Host "`nSample Mapped Products:" -ForegroundColor Yellow
    $dt | Select-Object -First 15 ProductCode, ProductName, MappedSupplierCode, MappedSupplierName | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

# Test UOM and IsWeighable Detection Rules across Sigma 21 Products
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
        p.ID AS ProductCode,
        p.Name AS ProductName,
        ISNULL(p.TamilName, N'') AS TamilName,
        p.Weight,
        p.Box,
        p.Pcs,
        CASE 
            -- Check explicit Weight column in Sigma
            WHEN p.Weight > 0 THEN 'Kgs'
            -- Check Keywords in Name or TamilName
            WHEN p.Name LIKE '%VELLAM%' OR p.Name LIKE '%RICE%' OR p.Name LIKE '%PARUPPU%' OR p.Name LIKE '%SUGAR%'
              OR p.Name LIKE '%DHAL%' OR p.Name LIKE '%DAL%' OR p.Name LIKE '%ATTA%' OR p.Name LIKE '%MAIDA%' OR p.Name LIKE '%RAVA%'
              OR p.Name LIKE '%KG%' OR p.Name LIKE '%1K%' OR p.Name LIKE '%2K%' OR p.Name LIKE '%5K%' OR p.Name LIKE '%10K%' OR p.Name LIKE '%25K%'
              OR p.Name LIKE '%500G%' OR p.Name LIKE '%250G%' OR p.Name LIKE '%100G%' OR p.Name LIKE '%50G%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%'
              OR p.Name LIKE '%KILO%' OR p.Name LIKE '%LOOSE%' OR p.Name LIKE '%OIL%' OR p.Name LIKE '%GHEE%' OR p.Name LIKE '%SALT%'
              OR p.TamilName LIKE N'%கி%' OR p.TamilName LIKE N'%கிலோ%' OR p.TamilName LIKE N'%வெல்லம்%' OR p.TamilName LIKE N'%அரிசி%' OR p.TamilName LIKE N'%பருப்பு%'
            THEN 'Kgs'
            WHEN p.Box = 1 THEN 'Box'
            ELSE 'Pcs'
        END AS ResolvedUom,
        CASE 
            WHEN p.Weight > 0 
              OR p.Name LIKE '%VELLAM%' OR p.Name LIKE '%RICE%' OR p.Name LIKE '%PARUPPU%' OR p.Name LIKE '%SUGAR%'
              OR p.Name LIKE '%DHAL%' OR p.Name LIKE '%DAL%' OR p.Name LIKE '%ATTA%' OR p.Name LIKE '%MAIDA%' OR p.Name LIKE '%RAVA%'
              OR p.Name LIKE '%KG%' OR p.Name LIKE '%1K%' OR p.Name LIKE '%2K%' OR p.Name LIKE '%5K%' OR p.Name LIKE '%10K%' OR p.Name LIKE '%25K%'
              OR p.Name LIKE '%500G%' OR p.Name LIKE '%250G%' OR p.Name LIKE '%100G%' OR p.Name LIKE '%50G%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%'
              OR p.Name LIKE '%KILO%' OR p.Name LIKE '%LOOSE%' OR p.Name LIKE '%OIL%' OR p.Name LIKE '%GHEE%' OR p.Name LIKE '%SALT%'
              OR p.TamilName LIKE N'%கி%' OR p.TamilName LIKE N'%கிலோ%' OR p.TamilName LIKE N'%வெல்லம்%' OR p.TamilName LIKE N'%அரிசி%' OR p.TamilName LIKE N'%பருப்பு%'
            THEN 1
            ELSE 0
        END AS IsWeighable
    FROM Master_Inventory_Product p
    WHERE p.Status = 1;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 300
    $cmd.CommandText = $sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null

    $total = $dt.Rows.Count
    $kgsCount = ($dt.Select("ResolvedUom = 'Kgs'")).Count
    $pcsCount = ($dt.Select("ResolvedUom = 'Pcs'")).Count
    $boxCount = ($dt.Select("ResolvedUom = 'Box'")).Count

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " UOM SCAN RESULTS FOR SIGMA 21 PRODUCTS" -ForegroundColor Green
    Write-Host " Total Active Products: $total" -ForegroundColor Yellow
    Write-Host " Mapped to Kgs (Weighable / Loose): $kgsCount ($([math]::Round(($kgsCount/$total)*100, 2))%)" -ForegroundColor Green
    Write-Host " Mapped to Pcs (Standard Packaged): $pcsCount ($([math]::Round(($pcsCount/$total)*100, 2))%)" -ForegroundColor Cyan
    Write-Host " Mapped to Box: $boxCount" -ForegroundColor Magenta
    Write-Host "==================================================" -ForegroundColor Green

    Write-Host "`nSample Weighable (Kgs) Items (e.g. A VELLAM 1K):" -ForegroundColor Yellow
    $dt | Where-Object { $_.ResolvedUom -eq 'Kgs' } | Select-Object -First 20 ProductCode, ProductName, TamilName, ResolvedUom, IsWeighable | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

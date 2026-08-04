# Analyze Sigma 21 Customer Master and Customer Ledger Balances
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
    Write-Host "[INFO] Connected to Sigma 21 ($Database)..." -ForegroundColor Cyan

    # 1. Count Customer Accounts in Master_Accounts
    $sqlCustomers = @"
    SELECT 
        COUNT(*) AS TotalCustomers,
        COUNT(CASE WHEN Mobile1 IS NOT NULL AND LEN(LTRIM(RTRIM(Mobile1))) > 0 THEN 1 END) AS WithMobile1
    FROM Master_Accounts
    WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%';
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sqlCustomers
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dtCust = New-Object System.Data.DataTable
    $adapter.Fill($dtCust) | Out-Null

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " SIGMA 21 CUSTOMER MASTER STATS" -ForegroundColor Green
    Write-Host " Total Customer Accounts: $($dtCust.Rows[0]['TotalCustomers'])" -ForegroundColor Yellow
    Write-Host " Customers with Mobile1: $($dtCust.Rows[0]['WithMobile1'])" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Green

    # All Customer Accounts
    $sqlSample = @"
    SELECT 
        ID AS CustomerCode,
        Name,
        ISNULL(PetName, N'') AS TamilName,
        ISNULL(Mobile1, N'') AS Mobile1,
        ISNULL(Address1, N'') AS Address1
    FROM Master_Accounts
    WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%'
    ORDER BY Name;
"@
    $cmd.CommandText = $sqlSample
    $dtSample = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtSample) | Out-Null
    
    Write-Host "`nSigma 21 Customer Accounts List:" -ForegroundColor Yellow
    $dtSample | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    # Also check all AccountType / FormName values in Master_Accounts to see if there are more customers!
    $sqlAccountTypes = @"
    SELECT 
        FormName,
        AccountType,
        COUNT(*) AS AccountCount
    FROM Master_Accounts
    GROUP BY FormName, AccountType
    ORDER BY AccountCount DESC;
"@
    $cmd.CommandText = $sqlAccountTypes
    $dtTypes = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtTypes) | Out-Null

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " MASTER ACCOUNTS BREAKDOWN BY TYPE" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green
    $dtTypes | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Green

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

# Test combined Customer Master & Ledger extraction from Sigma 21
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
    Write-Host "[INFO] Querying Combined Customer Records from Sigma 21..." -ForegroundColor Cyan

    $sqlCombined = @"
    SELECT 
        CustomerCode,
        Name,
        TamilName,
        Phone,
        Email,
        Address,
        LoyaltyPoints,
        LedgerBalance
    FROM (
        -- Source 1: CRM Loyalty & Retail Customers (14,341 records)
        SELECT 
            ID AS CustomerCode,
            LTRIM(RTRIM(Name)) AS Name,
            ISNULL(PetName, N'') AS TamilName,
            ISNULL(Mobile1, ISNULL(Mobile2, ISNULL(Phone1, N'0000000000'))) AS Phone,
            ISNULL(Email, N'') AS Email,
            ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
            CAST(ISNULL(BalancePoint, 0) AS DECIMAL(18,2)) AS LoyaltyPoints,
            CAST(ISNULL(Balance, 0) AS DECIMAL(18,2)) AS LedgerBalance,
            ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(Name)) ORDER BY ID DESC) AS rnk
        FROM Master_CRM_PointsCustomer
        WHERE Name IS NOT NULL AND LEN(LTRIM(RTRIM(Name))) > 0

        UNION ALL

        -- Source 2: Sundry Debtors from Master_Accounts (19 records)
        SELECT 
            ID AS CustomerCode,
            LTRIM(RTRIM(Name)) AS Name,
            ISNULL(PetName, N'') AS TamilName,
            ISNULL(Mobile1, ISNULL(Phone1, N'0000000000')) AS Phone,
            ISNULL(Email, N'') AS Email,
            ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
            0.00 AS LoyaltyPoints,
            0.00 AS LedgerBalance,
            1 AS rnk
        FROM Master_Accounts
        WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%'
    ) combined
    WHERE rnk = 1;
"@

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sqlCombined
    $cmd.CommandTimeout = 120
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dtCombined = New-Object System.Data.DataTable
    $adapter.Fill($dtCombined) | Out-Null

    $total = $dtCombined.Rows.Count
    $withPhone = ($dtCombined.Select("Phone <> '0000000000' AND Phone <> ''")).Count
    $withPoints = ($dtCombined.Select("LoyaltyPoints > 0")).Count
    $withBalance = ($dtCombined.Select("LedgerBalance <> 0")).Count

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " COMBINED SIGMA 21 CUSTOMER MIGRATION STATS" -ForegroundColor Green
    Write-Host " Total Unique Customers Extracted: $total" -ForegroundColor Yellow
    Write-Host " Customers with Valid Phone Numbers: $withPhone" -ForegroundColor Cyan
    Write-Host " Customers with Active Loyalty Points: $withPoints" -ForegroundColor Magenta
    Write-Host " Customers with Ledger Balances: $withBalance" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green

    Write-Host "`nSample 20 Extracted Customers:" -ForegroundColor Yellow
    $dtCombined | Select-Object -First 20 CustomerCode, Name, Phone, LoyaltyPoints, LedgerBalance | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

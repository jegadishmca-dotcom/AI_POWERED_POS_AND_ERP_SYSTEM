# Inspect non-zero Customer Balances in Sigma 21
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
    Write-Host "[INFO] Inspecting Customer Balances in Sigma 21..." -ForegroundColor Cyan

    # 1. Non-zero balances in Master_CRM_PointsCustomer
    $sqlCrmBal = @"
    SELECT 
        ID, Name, CustomerID, Mobile1, Balance, Debit, Credit, BalancePoint
    FROM Master_CRM_PointsCustomer
    WHERE ISNULL(Balance, 0) <> 0 OR ISNULL(Debit, 0) <> 0 OR ISNULL(Credit, 0) <> 0
    ORDER BY ABS(Balance) DESC;
"@
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sqlCrmBal
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dtCrmBal = New-Object System.Data.DataTable
    $adapter.Fill($dtCrmBal) | Out-Null

    Write-Host "`nNon-Zero Customer Balances in Master_CRM_PointsCustomer: $($dtCrmBal.Rows.Count)" -ForegroundColor Yellow
    if ($dtCrmBal.Rows.Count -gt 0) {
        $dtCrmBal | Select-Object -First 15 | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Green
    }

    # 2. Non-zero balances in CustomerBalance table
    $sqlCustBal = @"
    SELECT 
        Name, Type, Category, Debit, Credit, Balance
    FROM CustomerBalance
    WHERE ISNULL(Balance, 0) <> 0 OR ISNULL(Debit, 0) <> 0 OR ISNULL(Credit, 0) <> 0
    ORDER BY ABS(Balance) DESC;
"@
    $cmd.CommandText = $sqlCustBal
    $dtCustBal = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtCustBal) | Out-Null

    Write-Host "`nNon-Zero Balances in CustomerBalance Table: $($dtCustBal.Rows.Count)" -ForegroundColor Yellow
    if ($dtCustBal.Rows.Count -gt 0) {
        $dtCustBal | Select-Object -First 20 | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Cyan
    }

    # 3. Sum of Net Customer Sales / Credit Balances from Trans_Inventory_SOM
    $sqlTransBal = @"
    SELECT TOP 15
        a.ID AS CustomerCode,
        a.Name AS CustomerName,
        SUM(CASE WHEN t.FormName IN ('Sales', 'DeliveryNote') THEN t.Amount ELSE -t.Amount END) AS TotalSalesAmount,
        COUNT(t.VNO) AS BillCount
    FROM Master_Accounts a
    INNER JOIN Trans_Inventory_SOM t ON a.ID = t.Account
    WHERE a.FormName = 'Customer' OR a.AccountType = 'Sundry Debtors' OR a.AccountType LIKE '%Debtor%'
    GROUP BY a.ID, a.Name
    ORDER BY TotalSalesAmount DESC;
"@
    $cmd.CommandText = $sqlTransBal
    $dtTransBal = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $adapter.Fill($dtTransBal) | Out-Null

    Write-Host "`nTop Customer Ledger Transaction Totals:" -ForegroundColor Yellow
    $dtTransBal | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Magenta

    $conn.Close()
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
}

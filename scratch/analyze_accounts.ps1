# Analyze AccountTypes and sample Customers and Suppliers
$connStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;Connect Timeout=10;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

Write-Host "=== Account Types in Master_Accounts ===" -ForegroundColor Yellow
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT AccountType, AccountCategory, FormName, COUNT(*) as TotalCount FROM Master_Accounts GROUP BY AccountType, AccountCategory, FormName ORDER BY TotalCount DESC;"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  Type: $($reader['AccountType']) | Category: $($reader['AccountCategory']) | Form: $($reader['FormName']) | Count: $($reader['TotalCount'])"
}
$reader.Close()

Write-Host "`n=== Sample Customers (Top 5) ===" -ForegroundColor Yellow
$cmd.CommandText = "SELECT TOP 5 ID, Name, Mobile1, Phone1, GSTNO, CreditLimit, Balance FROM Master_Accounts WHERE AccountType LIKE '%Customer%' OR AccountType LIKE '%Debtor%' OR FormName LIKE '%Customer%';"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  ID: $($reader['ID']) | Name: $($reader['Name']) | Mobile: $($reader['Mobile1']) | GST: $($reader['GSTNO']) | Bal: $($reader['Balance'])"
}
$reader.Close()

Write-Host "`n=== Sample Suppliers / Vendors (Top 5) ===" -ForegroundColor Yellow
$cmd.CommandText = "SELECT TOP 5 ID, Name, Mobile1, Phone1, GSTNO, CreditLimit, Balance FROM Master_Accounts WHERE AccountType LIKE '%Supplier%' OR AccountType LIKE '%Creditor%' OR AccountType LIKE '%Vendor%' OR FormName LIKE '%Supplier%';"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  ID: $($reader['ID']) | Name: $($reader['Name']) | Mobile: $($reader['Mobile1']) | GST: $($reader['GSTNO']) | Bal: $($reader['Balance'])"
}
$reader.Close()

$conn.Close()

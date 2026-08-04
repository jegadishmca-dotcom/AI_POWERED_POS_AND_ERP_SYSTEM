# Inspect Trans_Accounts and Trans_Accounts_Finance schema
$connStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;Connect Timeout=10;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

Write-Host "Columns in Trans_Accounts:" -ForegroundColor Yellow
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Trans_Accounts';"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host " - $($reader['COLUMN_NAME']) ($($reader['DATA_TYPE']))"
}
$reader.Close()

Write-Host "`nSample Trans_Accounts Rows (Top 5):" -ForegroundColor Yellow
$cmd.CommandText = "SELECT TOP 5 VNo, VDate, AccountName, Type, Debit, Credit, Narration, Balance FROM Trans_Accounts ORDER BY AID DESC;"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  VNo: $($reader['VNo']) | Date: $($reader['VDate']) | Account: $($reader['AccountName']) | Type: $($reader['Type']) | Debit: $($reader['Debit']) | Credit: $($reader['Credit']) | Bal: $($reader['Balance'])"
}
$reader.Close()

$conn.Close()

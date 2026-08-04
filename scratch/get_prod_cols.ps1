# Get exact column names for Master_Inventory_Product
$connStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;Connect Timeout=10;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Master_Inventory_Product';"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host " - $($reader['COLUMN_NAME'])"
}
$reader.Close()

$conn.Close()

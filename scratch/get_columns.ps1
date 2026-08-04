# Get exact column names for Master_Accounts and Master_Accounts_Type
$connStr = "Server=192.168.1.10;Database=APPLE26-27;User Id=sa;Password=Q7!mX#92Lp@Tz4Ks;TrustServerCertificate=True;Connect Timeout=10;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

function GetCols($tbl) {
    Write-Host "`nColumns in ${tbl}:" -ForegroundColor Yellow
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$tbl';"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host " - $($reader['COLUMN_NAME']) ($($reader['DATA_TYPE']))"
    }
    $reader.Close()
}

GetCols "Master_Accounts"
GetCols "Master_Accounts_Type"

$conn.Close()

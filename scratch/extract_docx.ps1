Add-Type -AssemblyName System.IO.Compression.FileSystem
$docxPath = "d:\JEGADISH\APPLE_SUPERMARKET_POS_PROJECT\AI_POWERED_POS_AND_ERP_SYSTEM\Enterprise_Supermarket_POS&ERP_System-Deployment_Guide.docx"
$zip = [System.IO.Compression.ZipFile]::OpenRead($docxPath)
$entry = $zip.GetEntry("word/document.xml")
$stream = $entry.Open()
$reader = New-Object System.IO.StreamReader($stream)
$xml = $reader.ReadToEnd()
$reader.Close()
$stream.Close()
$zip.Dispose()
$text = $xml -replace '<[^>]+>', ' '
$text = $text -replace '\s+', ' '
Write-Output $text

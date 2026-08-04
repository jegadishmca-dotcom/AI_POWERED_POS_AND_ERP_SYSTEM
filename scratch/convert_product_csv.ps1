# =====================================================
# Convert SSMS Fixed-Width Export to Proper CSV
# For: Apple Supermarket Product Master Import
# =====================================================

$inputFile = "D:\JEGADISH\APPLE_SUPERMARKET_POS_PROJECT\DOCS\PRODUCT MASTER - APPLE SUPERMARKET 24-JULY-2026\product_master_export.CSV"
$outputFile = "D:\JEGADISH\APPLE_SUPERMARKET_POS_PROJECT\DOCS\PRODUCT MASTER - APPLE SUPERMARKET 24-JULY-2026\product_master_CLEAN.csv"

Write-Host "[INFO] Reading fixed-width export file..." -ForegroundColor Cyan
$lines = [System.IO.File]::ReadAllLines($inputFile, [System.Text.Encoding]::UTF8)

# Parse column boundaries from the separator line (line 2, index 1)
$sepLine = $lines[1]
$columns = @()
$pos = 0
$inDash = $false
$start = 0

for ($i = 0; $i -le $sepLine.Length; $i++) {
    $ch = if ($i -lt $sepLine.Length) { $sepLine[$i] } else { ' ' }
    if ($ch -eq '-' -and -not $inDash) {
        $inDash = $true
        $start = $i
    }
    elseif ($ch -ne '-' -and $inDash) {
        $inDash = $false
        $columns += [PSCustomObject]@{ Start = $start; Length = ($i - $start) }
    }
}

Write-Host "[INFO] Detected $($columns.Count) columns" -ForegroundColor Cyan
for ($c = 0; $c -lt $columns.Count; $c++) {
    $headerVal = $lines[0].Substring($columns[$c].Start, [Math]::Min($columns[$c].Length, $lines[0].Length - $columns[$c].Start)).Trim()
    Write-Host "  Column $($c+1): '$headerVal' (pos $($columns[$c].Start), len $($columns[$c].Length))"
}

# Build CSV output
$csvLines = [System.Collections.Generic.List[string]]::new()
$csvLines.Add("ProductCode,Name,TamilName,Description,Mrp,SellingPrice,PurchasePrice,Barcode,TaxSlabName,IsWeighable,HasExpiry,Uom")

$imported = 0
$skipped = 0
$fallbackCount = 0

for ($i = 2; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Skip empty lines, footer lines
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -match 'rows affected') { continue }
    if ($line -match 'Completion time') { continue }
    if ($line.Length -lt 20) { continue }

    # Extract each column using fixed positions
    $values = @()
    for ($c = 0; $c -lt $columns.Count; $c++) {
        $colStart = $columns[$c].Start
        $colLen = $columns[$c].Length
        if ($colStart -lt $line.Length) {
            $actualLen = [Math]::Min($colLen, $line.Length - $colStart)
            $val = $line.Substring($colStart, $actualLen).Trim()
        } else {
            $val = ""
        }
        $values += $val
    }

    # Map: 0=ProductCode, 1=Name, 2=TamilName, 3=Description, 4=Mrp, 5=SellingPrice, 6=PurchasePrice, 7=Barcode, 8=TaxSlabName, 9=IsWeighable, 10=HasExpiry, 11=Uom
    $productCode = $values[0]
    $name = $values[1]
    $tamilName = $values[2]
    $description = $values[3]
    $mrp = $values[4]
    $sellingPrice = $values[5]
    $purchasePrice = $values[6]
    $barcode = $values[7]
    $taxSlabName = $values[8]
    $isWeighable = $values[9]
    $hasExpiry = $values[10]
    $uom = if ($values.Count -gt 11) { $values[11] } else { "Pcs" }

    # Skip if product code is empty
    if ([string]::IsNullOrWhiteSpace($productCode)) { $skipped++; continue }

    # Clean up Name - remove commas and quotes that would break CSV
    $name = $name -replace '"', '""'
    $tamilName = $tamilName -replace '"', '""'
    $description = $description -replace '"', '""'

    # Validate/fix prices
    $mrpVal = 0; [decimal]::TryParse($mrp, [ref]$mrpVal) | Out-Null
    $sellVal = 0; [decimal]::TryParse($sellingPrice, [ref]$sellVal) | Out-Null
    $purchVal = 0; [decimal]::TryParse($purchasePrice, [ref]$purchVal) | Out-Null

    if ($mrpVal -le 0) { $mrpVal = 1.00; $fallbackCount++ }
    if ($sellVal -le 0) { $sellVal = $mrpVal }
    if ($sellVal -gt $mrpVal) { $sellVal = $mrpVal }  # Cap selling at MRP
    if ($purchVal -le 0) { $purchVal = [Math]::Round($sellVal * 0.80, 2) }

    # Fix TaxSlabName - ensure it's a valid name
    if ([string]::IsNullOrWhiteSpace($taxSlabName) -or $taxSlabName -notmatch '^GST') {
        $taxSlabName = "GST 0%"
    }

    # Build CSV line (quote fields that might contain commas)
    $csvLine = "$productCode,`"$name`",`"$tamilName`",`"$description`",$mrpVal,$sellVal,$purchVal,$barcode,$taxSlabName,$isWeighable,$hasExpiry,$uom"
    $csvLines.Add($csvLine)
    $imported++
}

# Write output with UTF-8 BOM (for Excel/import compatibility)
$utf8Bom = [System.Text.UTF8Encoding]::new($true)
[System.IO.File]::WriteAllLines($outputFile, $csvLines.ToArray(), $utf8Bom)

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Conversion Complete!" -ForegroundColor Green  
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Total products exported: $imported" -ForegroundColor Cyan
Write-Host "  Skipped (empty/invalid): $skipped" -ForegroundColor Yellow
Write-Host "  Fallback price (1.00):   $fallbackCount" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Output file: $outputFile" -ForegroundColor Green
Write-Host ""

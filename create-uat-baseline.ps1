# ==========================================
# AI UAT Platform - Baseline Folder Generator
# ==========================================

$ProjectRoot = Get-Location

$Base = Join-Path $ProjectRoot "tests\uat-engine"

$Folders = @(

"baselines",

"baselines\Sales",
"baselines\Purchasing",
"baselines\Inventory",
"baselines\Finance",
"baselines\CRM",
"baselines\Loyalty",
"baselines\GST",
"baselines\Reports",
"baselines\Security",

"artifacts",

"artifacts\screenshots",
"artifacts\videos",
"artifacts\traces",
"artifacts\logs",
"artifacts\reports"

)

foreach ($folder in $Folders)
{
    $path = Join-Path $Base $folder

    if (!(Test-Path $path))
    {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
        Write-Host "Created $path" -ForegroundColor Green
    }
    else
    {
        Write-Host "Exists  $path" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Baseline root created successfully." -ForegroundColor Cyan
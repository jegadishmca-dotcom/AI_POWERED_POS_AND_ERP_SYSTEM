param(

[Parameter(Mandatory=$true)]

[string]$Module,

[Parameter(Mandatory=$true)]

[string]$Scenario

)

$ProjectRoot = Get-Location

$Base = Join-Path $ProjectRoot "tests\uat-engine\baselines"

$ScenarioFolder = Join-Path $Base "$Module\$Scenario"

$Folders = @(

"database",
"evidence",
"receipt",
"screenshots",
"metadata"

)

foreach($folder in $Folders)
{
    New-Item -ItemType Directory `
        -Force `
        -Path (Join-Path $ScenarioFolder $folder) | Out-Null
}

New-Item -ItemType File `
    -Force `
    -Path (Join-Path $ScenarioFolder "README.md") | Out-Null

New-Item -ItemType File `
    -Force `
    -Path (Join-Path $ScenarioFolder "baseline.json") | Out-Null

Write-Host ""
Write-Host "Scenario Created"
Write-Host ""
Write-Host $ScenarioFolder
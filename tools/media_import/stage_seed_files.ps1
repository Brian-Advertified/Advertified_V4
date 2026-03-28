param(
    [string]$SourceDir = "C:\Users\CC KEMPTON\Downloads",
    [string]$DestinationDir = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$requiredFiles = @(
    "import_manifest.csv",
    "raw_pdf_pages.csv",
    "package_document_metadata.csv",
    "ooh_inventory_blackspace.csv",
    "package_summary_seed.csv",
    "radio_slot_grid_seed.csv",
    "sabc_rate_table_seed.csv"
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

if ([string]::IsNullOrWhiteSpace($DestinationDir)) {
    $DestinationDir = Join-Path $repoRoot "database\media_import\seed"
}

if (-not (Test-Path -LiteralPath $SourceDir)) {
    throw "Source directory not found: $SourceDir"
}

if (-not (Test-Path -LiteralPath $DestinationDir)) {
    New-Item -ItemType Directory -Path $DestinationDir | Out-Null
}

$missing = @()
foreach ($fileName in $requiredFiles) {
    $sourcePath = Join-Path $SourceDir $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        $missing += $fileName
    }
}

if ($missing.Count -gt 0) {
    throw "Missing required seed files in '$SourceDir': $($missing -join ', ')"
}

foreach ($fileName in $requiredFiles) {
    $sourcePath = Join-Path $SourceDir $fileName
    $destinationPath = Join-Path $DestinationDir $fileName

    if ((Test-Path -LiteralPath $destinationPath) -and -not $Force) {
        Write-Host "Skipping existing file: $fileName" -ForegroundColor Yellow
        continue
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force:$Force
    Write-Host "Staged $fileName" -ForegroundColor Green
}

Write-Host ""
Write-Host "Seed files are ready in: $DestinationDir" -ForegroundColor Cyan

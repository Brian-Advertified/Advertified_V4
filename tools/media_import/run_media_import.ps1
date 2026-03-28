param(
    [string]$SeedDir = "",
    [string]$DatabaseName = "Advertified",
    [string]$DatabaseHost = "localhost",
    [int]$DatabasePort = 5432,
    [string]$DatabaseUser = "postgres",
    [string]$DatabasePassword = "Gomo2004!@#",
    [string]$PythonPath = "",
    [string]$PsqlPath = "",
    [switch]$SkipNormalize
)

$ErrorActionPreference = "Stop"

function Get-RequiredCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Required command '$Name' was not found in PATH."
    }

    return $command.Source
}

function Resolve-ExecutablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PreferredPath,
        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        if (-not (Test-Path -LiteralPath $PreferredPath)) {
            throw "Configured path for '$CommandName' does not exist: $PreferredPath"
        }

        return (Resolve-Path -LiteralPath $PreferredPath).Path
    }

    return Get-RequiredCommand -Name $CommandName
}

function Convert-ToPsqlPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    return ($resolved -replace "\\", "/")
}

function Invoke-PsqlFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$DbHost,
        [Parameter(Mandatory = $true)]
        [int]$DbPort,
        [Parameter(Mandatory = $true)]
        [string]$DbName,
        [Parameter(Mandatory = $true)]
        [string]$DbUser,
        [Parameter(Mandatory = $true)]
        [string]$DbPassword,
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    $env:PGPASSWORD = $DbPassword
    try {
        & $ExecutablePath -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -f $FilePath
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed for file: $FilePath"
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Invoke-PsqlCopy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$DbHost,
        [Parameter(Mandatory = $true)]
        [int]$DbPort,
        [Parameter(Mandatory = $true)]
        [string]$DbName,
        [Parameter(Mandatory = $true)]
        [string]$DbUser,
        [Parameter(Mandatory = $true)]
        [string]$DbPassword,
        [Parameter(Mandatory = $true)]
        [string]$CopyCommand
    )

    $env:PGPASSWORD = $DbPassword
    try {
        & $ExecutablePath -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c $CopyCommand
        if ($LASTEXITCODE -ne 0) {
            throw "psql copy command failed."
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$createTablesSql = Join-Path $repoRoot "database\media_import\001_create_tables.sql"
$copySeedSql = Join-Path $repoRoot "database\media_import\002_copy_seed_data.sql"
$transformSql = Join-Path $repoRoot "database\media_import\003_transform_final.sql"
$normalizerScript = Join-Path $repoRoot "tools\media_import\normalize_radio_from_raw_pages.py"

if ([string]::IsNullOrWhiteSpace($SeedDir)) {
    $SeedDir = Join-Path $repoRoot "database\media_import\seed"
}

$rawPdfPagesCsv = Join-Path $SeedDir "raw_pdf_pages.csv"

if (-not (Test-Path -LiteralPath $SeedDir)) {
    throw "Seed directory not found: $SeedDir"
}

if (-not (Test-Path -LiteralPath $rawPdfPagesCsv)) {
    throw "Expected raw PDF pages CSV not found: $rawPdfPagesCsv"
}

$psqlPath = Resolve-ExecutablePath -PreferredPath $PsqlPath -CommandName "psql"
$pythonExecutable = $null

if (-not $SkipNormalize) {
    $pythonExecutable = Resolve-ExecutablePath -PreferredPath $PythonPath -CommandName "python"
}

$seedDirForPsql = Convert-ToPsqlPath -Path $SeedDir
$copyCommands = @(
    "\copy import_manifest(source_file, channel, page_count) FROM '$seedDirForPsql/import_manifest.csv' WITH (FORMAT csv, HEADER true)",
    "\copy raw_import_pages(source_file, channel, page, page_text) FROM '$seedDirForPsql/raw_pdf_pages.csv' WITH (FORMAT csv, HEADER true)",
    "\copy package_document_metadata(source_file, channel, supplier_or_station, document_title, please_note) FROM '$seedDirForPsql/package_document_metadata.csv' WITH (FORMAT csv, HEADER true)",
    "\copy inventory_items(source_file, page, site_title, site_description, city_province, media_format, site_number, rate_card_zar, discounted_rate_zar, city_town, suburb, address, production_flighting_zar, material, illuminated, lsm, available, traffic_count, gps_coordinates, dimensions, kmz_file) FROM '$seedDirForPsql/ooh_inventory_blackspace.csv' WITH (FORMAT csv, HEADER true)",
    "\copy radio_packages(source_file, channel, supplier_or_station, element_name, exposure, value_zar, saving_or_discount_zar, investment_zar, duration, notes) FROM '$seedDirForPsql/package_summary_seed.csv' WITH (FORMAT csv, HEADER true)",
    "\copy radio_slot_grids(source_file, station, package_name, ad_length_seconds, exposure_per_month_text, spots_count, total_invoice_zar, package_cost_zar, avg_cost_per_spot_zar, monday_friday_windows, saturday_windows, sunday_windows, live_reads_allowed, terms_excerpt, notes, raw_grid_excerpt) FROM '$seedDirForPsql/radio_slot_grid_seed.csv' WITH (FORMAT csv, HEADER true, NULL '')",
    "\copy sabc_rate_tables(source_file, channel_type, product_name, package_cost_zar, spots_count, avg_cost_per_spot_zar, exposure_value_zar, audience_segment, date_range_text, notes, raw_excerpt) FROM '$seedDirForPsql/sabc_rate_table_seed.csv' WITH (FORMAT csv, HEADER true, NULL '')"
)

Invoke-Step -Label "Create staging and final tables" -Action {
    Invoke-PsqlFile -ExecutablePath $psqlPath -DbHost $DatabaseHost -DbPort $DatabasePort -DbName $DatabaseName -DbUser $DatabaseUser -DbPassword $DatabasePassword -FilePath $createTablesSql
}

if (-not $SkipNormalize) {
    Invoke-Step -Label "Generate normalized radio and SABC CSVs" -Action {
        & $pythonExecutable $normalizerScript $rawPdfPagesCsv $SeedDir
    }
}

Invoke-Step -Label "Load staging CSV data" -Action {
    Invoke-PsqlFile -ExecutablePath $psqlPath -DbHost $DatabaseHost -DbPort $DatabasePort -DbName $DatabaseName -DbUser $DatabaseUser -DbPassword $DatabasePassword -FilePath $copySeedSql
    foreach ($copyCommand in $copyCommands) {
        Invoke-PsqlCopy -ExecutablePath $psqlPath -DbHost $DatabaseHost -DbPort $DatabasePort -DbName $DatabaseName -DbUser $DatabaseUser -DbPassword $DatabasePassword -CopyCommand $copyCommand
    }
}

Invoke-Step -Label "Transform staging data into final tables" -Action {
    Invoke-PsqlFile -ExecutablePath $psqlPath -DbHost $DatabaseHost -DbPort $DatabasePort -DbName $DatabaseName -DbUser $DatabaseUser -DbPassword $DatabasePassword -FilePath $transformSql
}

Write-Host ""
Write-Host "Media import pipeline completed successfully." -ForegroundColor Green

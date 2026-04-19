param(
    [string]$AuditCsvPath = ".exports\ooh-price-audit.csv",
    [string]$OutputPath = ".exports\ooh-price-corrections.template.csv"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AuditCsvPath)) {
    throw "Audit CSV was not found: $AuditCsvPath"
}

$rows = Import-Csv -LiteralPath $AuditCsvPath
if ($rows.Count -eq 0) {
    throw "Audit CSV contains no rows: $AuditCsvPath"
}

$templateRows = foreach ($row in $rows) {
    [pscustomobject]@{
        supplier = $row.supplier
        source_file = $row.source_file
        current_site_name = $row.site_name
        current_media_type = $row.media_type
        current_city = $row.city
        current_suburb = $row.suburb
        current_province = $row.province
        current_site_code = $row.site_code
        current_discounted_rate_zar = $row.discounted_rate_zar
        current_rate_card_zar = $row.rate_card_zar
        flags = $row.flags
        corrected_site_name = ""
        corrected_media_type = ""
        corrected_city = ""
        corrected_suburb = ""
        corrected_province = ""
        corrected_site_code = ""
        corrected_discounted_rate_zar = ""
        corrected_rate_card_zar = ""
        correction_notes = ""
        verified_source = ""
        verified_by = ""
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$templateRows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
Write-Host "Wrote correction template to $OutputPath"

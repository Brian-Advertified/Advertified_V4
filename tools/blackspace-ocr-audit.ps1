param(
    [string]$PdfPath = "C:\Users\CC KEMPTON\Downloads\BlackSpace VSBLT_OOH Sites Q3_ 2025.pdf",
    [string]$Model = "gpt-5",
    [string]$ApiBase = "https://api.openai.com/v1",
    [string]$SshKeyPath = "C:\Users\CC KEMPTON\Downloads\advertified.pem",
    [string]$SshHost = "ubuntu@13.246.60.13",
    [string]$RemoteAppPath = "/home/ubuntu/apps/advertified-v4-dev",
    [string]$DatabaseContainer = "advertified-v4-dev-db-1",
    [string]$DatabaseName = "advertified_v4_dev",
    [string]$DatabaseUser = "advertified",
    [string]$ExportDirectory = ".exports\blackspace-ocr-audit",
    [switch]$SkipDatabaseAudit,
    [switch]$KeepUploadedFile,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

function Get-OpenAiApiKey {
    if ($env:OPENAI_API_KEY) { return $env:OPENAI_API_KEY }
    if ($env:OPENAI__ApiKey) { return $env:OPENAI__ApiKey }
    throw "OPENAI_API_KEY or OPENAI__ApiKey is required."
}

function Invoke-OpenAiFileUpload {
    param(
        [string]$ApiBase,
        [string]$ApiKey,
        [string]$FilePath
    )

    $fileName = [System.IO.Path]::GetFileName($FilePath)
    $httpClient = [System.Net.Http.HttpClient]::new()
    try {
        $httpClient.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $ApiKey)

        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $purposeContent = [System.Net.Http.StringContent]::new("user_data")
        $content.Add($purposeContent, "purpose")

        $fileBytes = [System.IO.File]::ReadAllBytes($FilePath)
        $fileContent = [System.Net.Http.ByteArrayContent]::new($fileBytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/pdf")
        $content.Add($fileContent, "file", $fileName)

        $response = $httpClient.PostAsync("$ApiBase/files", $content).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "OpenAI file upload failed: $($response.StatusCode) $responseBody"
        }

        return $responseBody | ConvertFrom-Json
    }
    finally {
        $httpClient.Dispose()
    }
}

function Invoke-OpenAiJsonResponse {
    param(
        [string]$ApiBase,
        [string]$ApiKey,
        [string]$Model,
        [string]$FileId
    )

    $schema = @{
        type = "json_schema"
        name = "blackspace_ooh_extraction"
        strict = $true
        schema = @{
            type = "object"
            additionalProperties = $false
            properties = @{
                source_document = @{ type = "string" }
                extraction_notes = @{
                    type = "array"
                    items = @{ type = "string" }
                }
                placements = @{
                    type = "array"
                    items = @{
                        type = "object"
                        additionalProperties = $false
                        properties = @{
                            page = @{ type = "integer" }
                            site_name = @{ type = "string" }
                            media_type = @{ type = "string" }
                            city = @{ type = @("string","null") }
                            suburb = @{ type = @("string","null") }
                            province = @{ type = @("string","null") }
                            discounted_rate_zar = @{ type = @("number","null") }
                            rate_card_zar = @{ type = @("number","null") }
                            raw_price_text = @{ type = @("string","null") }
                            confidence = @{ type = "number" }
                            parse_warnings = @{
                                type = "array"
                                items = @{ type = "string" }
                            }
                        }
                        required = @(
                            "page",
                            "site_name",
                            "media_type",
                            "city",
                            "suburb",
                            "province",
                            "discounted_rate_zar",
                            "rate_card_zar",
                            "raw_price_text",
                            "confidence",
                            "parse_warnings"
                        )
                    }
                }
            }
            required = @("source_document", "extraction_notes", "placements")
        }
    }

    $prompt = @"
You are extracting OOH inventory rows from a supplier PDF for audit purposes.

Read the entire attached PDF end to end and extract every priced OOH placement row you can find.

Rules:
- Return one placement per priced row in the PDF.
- Preserve wording as seen in the PDF wherever possible.
- Do not invent missing values.
- Put null for city/suburb/province/discounted_rate_zar/rate_card_zar/raw_price_text when genuinely unavailable.
- If multiple prices appear, use the discounted/sellable price as discounted_rate_zar and the higher crossed-out or rate-card value as rate_card_zar when clearly shown.
- Add parse_warnings when the row is ambiguous, merged across columns, or partially unreadable.
- confidence must be between 0 and 1.
- The output must be valid JSON matching the schema exactly.
"@

    $body = @{
        model = $Model
        input = @(
            @{
                role = "user"
                content = @(
                    @{
                        type = "input_file"
                        file_id = $FileId
                    },
                    @{
                        type = "input_text"
                        text = $prompt
                    }
                )
            }
        )
        text = @{
            format = $schema
        }
    } | ConvertTo-Json -Depth 30

    $headers = @{
        Authorization = "Bearer $ApiKey"
        "Content-Type" = "application/json"
    }

    $response = Invoke-RestMethod -Method Post -Uri "$ApiBase/responses" -Headers $headers -Body $body
    return $response
}

function Remove-OpenAiFile {
    param(
        [string]$ApiBase,
        [string]$ApiKey,
        [string]$FileId
    )

    try {
        $headers = @{ Authorization = "Bearer $ApiKey" }
        Invoke-RestMethod -Method Delete -Uri "$ApiBase/files/$FileId" -Headers $headers | Out-Null
    }
    catch {
        Write-Warning "Could not delete uploaded OpenAI file $FileId. $_"
    }
}

function Get-ResponseOutputText {
    param($ResponseObject)

    if ($ResponseObject.PSObject.Properties.Name -contains "output_text" -and $ResponseObject.output_text) {
        return [string]$ResponseObject.output_text
    }

    foreach ($item in @($ResponseObject.output)) {
        if ($item.type -ne "message") { continue }
        foreach ($contentItem in @($item.content)) {
            if ($contentItem.type -eq "output_text" -and $contentItem.text) {
                return [string]$contentItem.text
            }
        }
    }

    throw "Could not find output_text in OpenAI response."
}

function Normalize-TextToken {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToLowerInvariant() -replace "[^a-z0-9]+", " ").Trim() -replace "\s+", " ")
}

function Normalize-SiteKey {
    param([string]$SiteName)
    if ([string]::IsNullOrWhiteSpace($SiteName)) { return "" }
    return (($SiteName.ToLowerInvariant() -replace "[^a-z0-9]+", "").Trim())
}

function Normalize-MediaTypeKey {
    param([string]$MediaType)
    $normalized = Normalize-TextToken $MediaType
    if ($normalized.Contains("screen")) { return "digital_screen" }
    return "billboard"
}

function Get-NullableString {
    param($Value)
    if ($null -eq $Value) { return "" }
    return [string]$Value
}

function Join-Warnings {
    param($Warnings)
    if ($null -eq $Warnings) { return "" }
    return (@($Warnings) -join ';')
}

function Get-BlackSpaceInventoryRows {
    param(
        [string]$SshKeyPath,
        [string]$SshHost,
        [string]$RemoteAppPath,
        [string]$DatabaseContainer,
        [string]$DatabaseName,
        [string]$DatabaseUser
    )

    $sql = @"
select
    supplier,
    coalesce(metadata_json->>'source_file', '') as source_file,
    site_name,
    media_type,
    city,
    suburb,
    province,
    metadata_json->>'discounted_rate_zar' as discounted_rate_zar,
    metadata_json->>'rate_card_zar' as rate_card_zar
from inventory_items_final
where lower(supplier) = 'blackspace'
order by site_name, media_type, coalesce((metadata_json->>'discounted_rate_zar')::numeric, 0) desc;
"@

    $remoteCommand = @"
cd $RemoteAppPath
cat <<'SQL' | docker exec -i $DatabaseContainer psql -U $DatabaseUser -d $DatabaseName -P footer=off -F ',' --csv
$sql
SQL
"@

    $csv = ssh -i $SshKeyPath $SshHost $remoteCommand
    return $csv | ConvertFrom-Csv
}

function Compare-BlackSpaceExtraction {
    param(
        [object[]]$ExtractedPlacements,
        [object[]]$InventoryRows
    )

    $comparisonRows = New-Object System.Collections.Generic.List[object]

    $inventoryLookup = @{}
    foreach ($row in $InventoryRows) {
        $key = "{0}|{1}|{2}|{3}" -f (Normalize-SiteKey $row.site_name), (Normalize-MediaTypeKey $row.media_type), (Get-NullableString $row.discounted_rate_zar), (Get-NullableString $row.rate_card_zar)
        if (-not $inventoryLookup.ContainsKey($key)) {
            $inventoryLookup[$key] = New-Object System.Collections.Generic.List[object]
        }
        $inventoryLookup[$key].Add($row)
    }

    $matchedInventoryKeys = New-Object System.Collections.Generic.HashSet[string]

    foreach ($placement in $ExtractedPlacements) {
        $discounted = if ($null -eq $placement.discounted_rate_zar) { "" } else { [string][decimal]$placement.discounted_rate_zar }
        $rateCard = if ($null -eq $placement.rate_card_zar) { "" } else { [string][decimal]$placement.rate_card_zar }
        $key = "{0}|{1}|{2}|{3}" -f (Normalize-SiteKey $placement.site_name), (Normalize-MediaTypeKey $placement.media_type), $discounted, $rateCard
        $status = if ($inventoryLookup.ContainsKey($key)) { "matched" } else { "missing_in_inventory" }
        if ($status -eq "matched") { [void]$matchedInventoryKeys.Add($key) }

        $comparisonRows.Add([pscustomobject]@{
            audit_status = $status
            source = "pdf_extraction"
            page = $placement.page
            site_name = $placement.site_name
            media_type = $placement.media_type
            city = $placement.city
            suburb = $placement.suburb
            province = $placement.province
            discounted_rate_zar = $placement.discounted_rate_zar
            rate_card_zar = $placement.rate_card_zar
            confidence = $placement.confidence
            warnings = Join-Warnings $placement.parse_warnings
        })
    }

    foreach ($row in $InventoryRows) {
        $key = "{0}|{1}|{2}|{3}" -f (Normalize-SiteKey $row.site_name), (Normalize-MediaTypeKey $row.media_type), (Get-NullableString $row.discounted_rate_zar), (Get-NullableString $row.rate_card_zar)
        if ($matchedInventoryKeys.Contains($key)) { continue }

        $comparisonRows.Add([pscustomobject]@{
            audit_status = "missing_in_pdf_extraction"
            source = "dev_inventory"
            page = $null
            site_name = $row.site_name
            media_type = $row.media_type
            city = $row.city
            suburb = $row.suburb
            province = $row.province
            discounted_rate_zar = $row.discounted_rate_zar
            rate_card_zar = $row.rate_card_zar
            confidence = $null
            warnings = ""
        })
    }

    return $comparisonRows
}

if (-not (Test-Path $PdfPath)) {
    throw "PDF not found: $PdfPath"
}

if (-not (Test-Path $ExportDirectory)) {
    New-Item -ItemType Directory -Path $ExportDirectory | Out-Null
}

$apiKey = Get-OpenAiApiKey

if ($ValidateOnly) {
    Write-Host "Validation OK"
    Write-Host "PDF: $PdfPath"
    Write-Host "Model: $Model"
    Write-Host "ExportDirectory: $ExportDirectory"
    if (-not $SkipDatabaseAudit) {
        Write-Host "Database audit enabled against DEV"
    }
    exit 0
}

$rawOpenAiPath = Join-Path $ExportDirectory "openai-response.json"
$rawExtractionPath = Join-Path $ExportDirectory "blackspace-extracted.json"
$extractedCsvPath = Join-Path $ExportDirectory "blackspace-extracted.csv"
$inventoryCsvPath = Join-Path $ExportDirectory "blackspace-dev-inventory.csv"
$auditCsvPath = Join-Path $ExportDirectory "blackspace-audit.csv"
$summaryPath = Join-Path $ExportDirectory "summary.txt"

Write-Host "Uploading PDF to OpenAI..."
$uploadedFile = Invoke-OpenAiFileUpload -ApiBase $ApiBase -ApiKey $apiKey -FilePath $PdfPath

try {
    Write-Host "Requesting OCR-assisted extraction from OpenAI..."
    $response = Invoke-OpenAiJsonResponse -ApiBase $ApiBase -ApiKey $apiKey -Model $Model -FileId $uploadedFile.id
    ($response | ConvertTo-Json -Depth 50) | Set-Content -Path $rawOpenAiPath -Encoding UTF8

    $outputText = Get-ResponseOutputText -ResponseObject $response
    $extraction = $outputText | ConvertFrom-Json
    ($extraction | ConvertTo-Json -Depth 50) | Set-Content -Path $rawExtractionPath -Encoding UTF8

    $extraction.placements | Export-Csv -Path $extractedCsvPath -NoTypeInformation -Encoding UTF8

    $summaryLines = New-Object System.Collections.Generic.List[string]
    $summaryLines.Add("Extracted placements: $($extraction.placements.Count)")
    $summaryLines.Add("Extraction notes: $((@($extraction.extraction_notes) -join ' | '))")

    if (-not $SkipDatabaseAudit) {
        Write-Host "Pulling live BlackSpace inventory rows from DEV..."
        $inventoryRows = Get-BlackSpaceInventoryRows -SshKeyPath $SshKeyPath -SshHost $SshHost -RemoteAppPath $RemoteAppPath -DatabaseContainer $DatabaseContainer -DatabaseName $DatabaseName -DatabaseUser $DatabaseUser
        $inventoryRows | Export-Csv -Path $inventoryCsvPath -NoTypeInformation -Encoding UTF8

        Write-Host "Comparing OCR extraction against DEV inventory..."
        $auditRows = Compare-BlackSpaceExtraction -ExtractedPlacements $extraction.placements -InventoryRows $inventoryRows
        $auditRows | Export-Csv -Path $auditCsvPath -NoTypeInformation -Encoding UTF8

        $grouped = $auditRows | Group-Object audit_status | Sort-Object Count -Descending
        foreach ($group in $grouped) {
            $summaryLines.Add("{0}: {1}" -f $group.Name, $group.Count)
        }
    }

    $summaryLines | Set-Content -Path $summaryPath -Encoding UTF8

    Write-Host "Saved raw response: $rawOpenAiPath"
    Write-Host "Saved extracted JSON: $rawExtractionPath"
    Write-Host "Saved extracted CSV: $extractedCsvPath"
    if (-not $SkipDatabaseAudit) {
        Write-Host "Saved DEV inventory CSV: $inventoryCsvPath"
        Write-Host "Saved audit CSV: $auditCsvPath"
    }
    Write-Host "Saved summary: $summaryPath"
}
finally {
    if (-not $KeepUploadedFile -and $uploadedFile.id) {
        Remove-OpenAiFile -ApiBase $ApiBase -ApiKey $apiKey -FileId $uploadedFile.id
    }
}

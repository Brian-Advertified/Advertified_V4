param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    [Parameter(Mandatory = $true)]
    [string]$SshKeyPath,

    [Parameter(Mandatory = $true)]
    [string]$SshHost,

    [string]$SshUser = "ubuntu",
    [string]$DbContainer = "advertified-v4-prod-db-1",
    [string]$Database = "advertified_v4_prod",
    [string]$DbUser = "advertified",
    [string]$ReplaceSupplier = "",
    [switch]$TruncateAll = $false
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CsvPath)) {
    throw "CSV was not found: $CsvPath"
}

$rows = Import-Csv -LiteralPath $CsvPath
if ($rows.Count -eq 0) {
    throw "CSV contains no rows: $CsvPath"
}

function Get-CleanValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $text = [string]$Value
    if ([string]::Equals($text, "NULL", [System.StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    return $text.Trim()
}

function Get-FieldValue {
    param(
        [pscustomobject]$Row,
        [string[]]$Names
    )

    foreach ($name in $Names) {
        $property = $Row.PSObject.Properties | Where-Object { $_.Name -eq $name } | Select-Object -First 1
        if ($null -ne $property) {
            return Get-CleanValue $property.Value
        }
    }

    return ""
}

function Convert-ToNullableBooleanText {
    param([object]$Value)

    $text = Get-CleanValue $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ""
    }

    switch ($text.ToLowerInvariant()) {
        "true" { return "true" }
        "false" { return "false" }
        "yes" { return "true" }
        "no" { return "false" }
        "1" { return "true" }
        "0" { return "false" }
        default { return "" }
    }
}

function Convert-ToNumberText {
    param([object]$Value)

    $text = Get-CleanValue $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ""
    }

    $normalized = $text -replace '[^0-9.\-]', ''
    return $normalized.Trim()
}

function Normalize-ProvinceName {
    param([object]$Value)

    $text = Get-CleanValue $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return ""
    }

    switch ($text.ToUpperInvariant()) {
        "EC" { return "Eastern Cape" }
        "FS" { return "Free State" }
        "GP" { return "Gauteng" }
        "KZN" { return "KwaZulu-Natal" }
        "LP" { return "Limpopo" }
        "MP" { return "Mpumalanga" }
        "NC" { return "Northern Cape" }
        "NW" { return "North West" }
        "WC" { return "Western Cape" }
        default { return $text }
    }
}

function Get-ProvinceFromSiteCode {
    param([string]$SiteCode)

    $text = Get-CleanValue $SiteCode
    if ([string]::IsNullOrWhiteSpace($text) -or $text.Length -lt 2) {
        return ""
    }

    $prefix = $text.Substring(0, [Math]::Min(3, $text.Length)).ToUpperInvariant()
    if ($prefix.StartsWith("GP")) { return "Gauteng" }
    if ($prefix.StartsWith("KZN")) { return "KwaZulu-Natal" }
    if ($prefix.StartsWith("WC")) { return "Western Cape" }
    if ($prefix.StartsWith("EC")) { return "Eastern Cape" }
    if ($prefix.StartsWith("NW")) { return "North West" }
    if ($prefix.StartsWith("FS")) { return "Free State" }
    if ($prefix.StartsWith("MP")) { return "Mpumalanga" }
    if ($prefix.StartsWith("NC")) { return "Northern Cape" }
    if ($prefix.StartsWith("LP")) { return "Limpopo" }
    return ""
}

function Convert-RawOohRow {
    param([pscustomobject]$Row)

    $supplier = Get-CleanValue $Row.supplier
    if ([string]::IsNullOrWhiteSpace($supplier)) {
        $supplier = "BlackSpace"
    }

    $isAvailable = "true"
    $availabilityStatus = Get-CleanValue $Row.availability_status
    if (-not [string]::IsNullOrWhiteSpace($availabilityStatus) -and $availabilityStatus.ToLowerInvariant() -in @("booked", "unavailable", "inactive")) {
        $isAvailable = "false"
    }

    $isActive = Convert-ToNullableBooleanText $Row.active
    if ($isActive -eq "false") {
        $isAvailable = "false"
    }

    $addressParts = @(
        (Get-CleanValue $Row.address_line1),
        (Get-CleanValue $Row.address_line2),
        (Get-CleanValue $Row.city),
        (Normalize-ProvinceName $Row.province_code)
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $noteParts = @(
        (Get-CleanValue $Row.description),
        (Get-CleanValue $Row.location_context),
        (Get-CleanValue $Row.visibility_notes),
        (Get-CleanValue $Row.audience_notes)
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $metadata = [ordered]@{
        row_id = Get-CleanValue $Row.row_id
        source_file_name = Get-CleanValue $Row.source_file_name
        source_version = Get-CleanValue $Row.source_version
        asset_type_code = Get-CleanValue $Row.asset_type_code
        channel_code = Get-CleanValue $Row.channel_code
        province_code = Get-CleanValue $Row.province_code
        address_line1 = Get-CleanValue $Row.address_line1
        address_line2 = Get-CleanValue $Row.address_line2
        postal_code = Get-CleanValue $Row.postal_code
        language_code = Get-CleanValue $Row.language_code
        audience_notes = Get-CleanValue $Row.audience_notes
        description = Get-CleanValue $Row.description
        location_context = Get-CleanValue $Row.location_context
        visibility_notes = Get-CleanValue $Row.visibility_notes
        dimensions_text = Get-CleanValue $Row.dimensions_text
        width_m = Convert-ToNumberText $Row.width_m
        height_m = Convert-ToNumberText $Row.height_m
        area_m2 = Convert-ToNumberText $Row.area_m2
        illuminated = Convert-ToNullableBooleanText $Row.illuminated
        power_backup = Convert-ToNullableBooleanText $Row.power_backup
        lsm_min = Convert-ToNumberText $Row.lsm_min
        lsm_max = Convert-ToNumberText $Row.lsm_max
        production_cost = Convert-ToNumberText $Row.production_cost
        availability_status = $availabilityStatus
        available_from = Get-CleanValue $Row.available_from
        available_to = Get-CleanValue $Row.available_to
        valid_from = Get-CleanValue $Row.valid_from
        valid_to = Get-CleanValue $Row.valid_to
        owned_priority_flag = Convert-ToNullableBooleanText $Row.owned_priority_flag
        active = $isActive
        import_source = [System.IO.Path]::GetFileName($CsvPath)
    }

    $metadataJson = ($metadata.GetEnumerator() |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.Value) } |
        ForEach-Object { @{ Key = $_.Key; Value = $_.Value } }) |
        ForEach-Object -Begin { $ordered = [ordered]@{} } -Process { $ordered[$_.Key] = $_.Value } -End { $ordered | ConvertTo-Json -Compress }

    if ([string]::IsNullOrWhiteSpace($metadataJson)) {
        $metadataJson = "{}"
    }

    return [pscustomobject]@{
        supplier                     = $supplier
        site_code                    = Get-CleanValue $Row.asset_code
        site_name                    = Get-CleanValue $Row.asset_name
        city                         = Get-CleanValue $Row.city
        suburb                       = Get-CleanValue $Row.suburb
        province                     = Normalize-ProvinceName $Row.province_code
        media_type                   = Get-CleanValue $Row.format_name
        address                      = ($addressParts -join ", ")
        latitude                     = Convert-ToNumberText $Row.latitude
        longitude                    = Convert-ToNumberText $Row.longitude
        is_available                 = $isAvailable
        discounted_rate_zar          = Convert-ToNumberText $Row.discounted_rate
        rate_card_zar                = Convert-ToNumberText $Row.standard_rate
        monthly_rate_zar             = Convert-ToNumberText $Row.standard_rate
        traffic_count                = Convert-ToNumberText $Row.traffic_count
        venue_type                   = Get-CleanValue $Row.venue_type
        premium_mass_fit             = ""
        price_positioning_fit        = ""
        audience_income_fit          = if ((Get-CleanValue $Row.lsm_min) -and (Get-CleanValue $Row.lsm_max)) { "lsm_$($Row.lsm_min)_$($Row.lsm_max)" } else { "" }
        youth_fit                    = ""
        family_fit                   = ""
        professional_fit             = ""
        commuter_fit                 = ""
        tourist_fit                  = ""
        high_value_shopper_fit       = ""
        audience_age_skew            = ""
        audience_gender_skew         = ""
        dwell_time_score             = ""
        environment_type             = Get-CleanValue $Row.asset_type_code
        buying_behaviour_fit         = ""
        primary_audience_tags_json   = "[]"
        secondary_audience_tags_json = "[]"
        recommendation_tags_json     = "[]"
        intelligence_notes           = ($noteParts -join " ")
        data_confidence              = "medium"
        updated_by                   = "raw_ooh_csv_import"
        metadata_json                = $metadataJson
    }
}

function Convert-SimpleOohRow {
    param([pscustomobject]$Row)

    $siteCode = Get-CleanValue $Row.site_code
    $siteName = Get-CleanValue $Row.site_name
    if ([string]::IsNullOrWhiteSpace($siteCode) -or $siteCode -eq "Site #") {
        return $null
    }

    if ([string]::IsNullOrWhiteSpace($siteName) -or $siteName -eq "Sites") {
        return $null
    }

    $suburb = Get-CleanValue $Row.suburb
    $supplier = Get-CleanValue $Row.supplier
    if ([string]::IsNullOrWhiteSpace($supplier)) {
        $supplier = "Eleven8"
    }

    $province = Normalize-ProvinceName $Row.province
    if ([string]::IsNullOrWhiteSpace($province)) {
        $province = Normalize-ProvinceName $Row.province_code
    }
    if ([string]::IsNullOrWhiteSpace($province)) {
        $province = Get-ProvinceFromSiteCode $siteCode
    }

    $city = Get-CleanValue $Row.city
    if ([string]::IsNullOrWhiteSpace($city)) {
        $city = $suburb
    }

    $monthlyRate = Convert-ToNumberText $Row.monthly_rate_zar
    if ([string]::IsNullOrWhiteSpace($monthlyRate)) {
        $monthlyRate = Convert-ToNumberText $Row.monthly_rate
    }

    $mediaType = Get-CleanValue $Row.media_type
    if ([string]::IsNullOrWhiteSpace($mediaType)) {
        $mediaType = "Digital Screen"
    }

    $metadata = [ordered]@{
        import_source = [System.IO.Path]::GetFileName($CsvPath)
    }

    $metadataJson = $metadata | ConvertTo-Json -Compress
    if ([string]::IsNullOrWhiteSpace($metadataJson)) {
        $metadataJson = "{}"
    }

    return [pscustomobject]@{
        supplier                     = $supplier
        site_code                    = $siteCode
        site_name                    = $siteName
        city                         = $city
        suburb                       = $suburb
        province                     = $province
        media_type                   = $mediaType
        address                      = Get-CleanValue $Row.address
        latitude                     = Convert-ToNumberText $Row.latitude
        longitude                    = Convert-ToNumberText $Row.longitude
        is_available                 = "true"
        discounted_rate_zar          = $monthlyRate
        rate_card_zar                = $monthlyRate
        monthly_rate_zar             = $monthlyRate
        traffic_count                = Convert-ToNumberText $Row.traffic_count
        venue_type                   = Get-CleanValue $Row.venue_type
        premium_mass_fit             = ""
        price_positioning_fit        = ""
        audience_income_fit          = ""
        youth_fit                    = ""
        family_fit                   = ""
        professional_fit             = ""
        commuter_fit                 = ""
        tourist_fit                  = ""
        high_value_shopper_fit       = ""
        audience_age_skew            = ""
        audience_gender_skew         = ""
        dwell_time_score             = ""
        environment_type             = ""
        buying_behaviour_fit         = ""
        primary_audience_tags_json   = "[]"
        secondary_audience_tags_json = "[]"
        recommendation_tags_json     = "[]"
        intelligence_notes           = Get-CleanValue $Row.intelligence_notes
        data_confidence              = "medium"
        updated_by                   = "simple_ooh_csv_import"
        metadata_json                = $metadataJson
    }
}

function Convert-ReadableOohRow {
    param([pscustomobject]$Row)

    $supplier = Get-FieldValue $Row @("Supplier", "supplier")
    $siteCode = Get-FieldValue $Row @("Site Code", "site_code")
    $siteName = Get-FieldValue $Row @("Site Name", "site_name")

    if ([string]::IsNullOrWhiteSpace($supplier) -or [string]::IsNullOrWhiteSpace($siteName)) {
        return $null
    }

    $metadata = [ordered]@{
        represented_inventory_count = Get-FieldValue $Row @("Represented Inventory Count", "inventory_rows")
        source_file_name = Get-FieldValue $Row @("Source File Name", "source_file_name")
        source_version = Get-FieldValue $Row @("Source Version", "source_version")
        import_source = Get-FieldValue $Row @("Import Source", "import_source")
    }

    $metadataJson = ($metadata.GetEnumerator() |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.Value) } |
        ForEach-Object -Begin { $ordered = [ordered]@{} } -Process { $ordered[$_.Key] = $_.Value } -End { $ordered | ConvertTo-Json -Compress })

    if ([string]::IsNullOrWhiteSpace($metadataJson)) {
        $metadataJson = "{}"
    }

    return [pscustomobject]@{
        supplier                     = $supplier
        site_code                    = $siteCode
        site_name                    = $siteName
        city                         = Get-FieldValue $Row @("City", "city")
        suburb                       = Get-FieldValue $Row @("Suburb / Area", "suburb")
        province                     = Normalize-ProvinceName (Get-FieldValue $Row @("Province", "province"))
        media_type                   = Get-FieldValue $Row @("Media Type", "media_type")
        address                      = Get-FieldValue $Row @("Address", "address")
        latitude                     = Convert-ToNumberText (Get-FieldValue $Row @("Latitude", "latitude"))
        longitude                    = Convert-ToNumberText (Get-FieldValue $Row @("Longitude", "longitude"))
        is_available                 = Convert-ToNullableBooleanText (Get-FieldValue $Row @("Available Now", "is_available"))
        discounted_rate_zar          = Convert-ToNumberText (Get-FieldValue $Row @("Discounted Rate (ZAR)", "discounted_rate_zar"))
        rate_card_zar                = Convert-ToNumberText (Get-FieldValue $Row @("Rate Card (ZAR)", "rate_card_zar"))
        monthly_rate_zar             = Convert-ToNumberText (Get-FieldValue $Row @("Monthly Rate (ZAR)", "monthly_rate_zar"))
        traffic_count                = Convert-ToNumberText (Get-FieldValue $Row @("Traffic Count", "traffic_count"))
        venue_type                   = Get-FieldValue $Row @("Venue Type", "venue_type")
        premium_mass_fit             = Get-FieldValue $Row @("Premium or Mass Fit", "premium_mass_fit")
        price_positioning_fit        = Get-FieldValue $Row @("Price Positioning Fit", "price_positioning_fit")
        audience_income_fit          = Get-FieldValue $Row @("Audience Income Fit", "audience_income_fit")
        youth_fit                    = Convert-ToNumberText (Get-FieldValue $Row @("Youth Fit", "youth_fit"))
        family_fit                   = Convert-ToNumberText (Get-FieldValue $Row @("Family Fit", "family_fit"))
        professional_fit             = Convert-ToNumberText (Get-FieldValue $Row @("Professional Fit", "professional_fit"))
        commuter_fit                 = Convert-ToNumberText (Get-FieldValue $Row @("Commuter Fit", "commuter_fit"))
        tourist_fit                  = Convert-ToNumberText (Get-FieldValue $Row @("Tourist Fit", "tourist_fit"))
        high_value_shopper_fit       = Convert-ToNumberText (Get-FieldValue $Row @("High Value Shopper Fit", "high_value_shopper_fit"))
        audience_age_skew            = Get-FieldValue $Row @("Audience Age Skew", "audience_age_skew")
        audience_gender_skew         = Get-FieldValue $Row @("Audience Gender Skew", "audience_gender_skew")
        dwell_time_score             = Convert-ToNumberText (Get-FieldValue $Row @("Dwell Time Score", "dwell_time_score"))
        environment_type             = Get-FieldValue $Row @("Environment Type", "environment_type")
        buying_behaviour_fit         = Get-FieldValue $Row @("Buying Behaviour Fit", "buying_behaviour_fit")
        primary_audience_tags_json   = Convert-ToJsonArrayText (Get-FieldValue $Row @("Primary Audience Tags", "primary_audience_tags"))
        secondary_audience_tags_json = Convert-ToJsonArrayText (Get-FieldValue $Row @("Secondary Audience Tags", "secondary_audience_tags"))
        recommendation_tags_json     = Convert-ToJsonArrayText (Get-FieldValue $Row @("Recommendation Tags", "recommendation_tags"))
        intelligence_notes           = Get-FieldValue $Row @("Planner Notes", "intelligence_notes")
        data_confidence              = Get-FieldValue $Row @("Data Confidence", "data_confidence")
        updated_by                   = Get-FieldValue $Row @("Updated By", "updated_by")
        metadata_json                = $metadataJson
    }
}

function Convert-ToJsonArrayText {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "[]"
    }

    $items = $Value.Split([char[]]@(';', ','), [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne "" }

    return ($items | ConvertTo-Json -Compress)
}

$tmpDirectory = Join-Path $PSScriptRoot "..\.tmp"
New-Item -ItemType Directory -Force -Path $tmpDirectory | Out-Null
$stageCsv = Join-Path $tmpDirectory "ooh_inventory_intelligence_stage.csv"

$isRawPlacementCsv = $rows[0].PSObject.Properties.Name -contains "asset_code"
$isSimpleSiteCsv = (-not $isRawPlacementCsv) -and ($rows[0].PSObject.Properties.Name -contains "site_code") -and ($rows[0].PSObject.Properties.Name -contains "monthly_rate")
$isReadableExportCsv = (-not $isRawPlacementCsv) -and (-not $isSimpleSiteCsv) -and ($rows[0].PSObject.Properties.Name -contains "Supplier") -and ($rows[0].PSObject.Properties.Name -contains "Site Code")

$stageRows = foreach ($row in $rows) {
    if ($isRawPlacementCsv) {
        Convert-RawOohRow -Row $row
        continue
    }

    if ($isSimpleSiteCsv) {
        $converted = Convert-SimpleOohRow -Row $row
        if ($null -ne $converted) {
            $converted
        }
        continue
    }

    if ($isReadableExportCsv) {
        $converted = Convert-ReadableOohRow -Row $row
        if ($null -ne $converted) {
            $converted
        }
        continue
    }

    $metadata = [ordered]@{}
    foreach ($property in $row.PSObject.Properties) {
        if ($property.Name -in @(
            "supplier", "site_code", "site_name", "city", "suburb", "province",
            "media_type", "address", "latitude", "longitude", "is_available",
            "discounted_rate_zar", "rate_card_zar", "monthly_rate_zar", "traffic_count",
            "venue_type", "premium_mass_fit", "price_positioning_fit", "audience_income_fit",
            "youth_fit", "family_fit", "professional_fit", "commuter_fit", "tourist_fit",
            "high_value_shopper_fit", "audience_age_skew", "audience_gender_skew",
            "dwell_time_score", "environment_type", "buying_behaviour_fit",
            "primary_audience_tags", "secondary_audience_tags", "recommendation_tags",
            "intelligence_notes", "data_confidence", "updated_by"
        )) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            $metadata[$property.Name] = [string]$property.Value
        }
    }

    $metadataJson = $metadata | ConvertTo-Json -Compress
    if ([string]::IsNullOrWhiteSpace($metadataJson)) {
        $metadataJson = "{}"
    }

    [pscustomobject]@{
        supplier                   = [string]$row.supplier
        site_code                  = [string]$row.site_code
        site_name                  = [string]$row.site_name
        city                       = [string]$row.city
        suburb                     = [string]$row.suburb
        province                   = [string]$row.province
        media_type                 = [string]$row.media_type
        address                    = [string]$row.address
        latitude                   = [string]$row.latitude
        longitude                  = [string]$row.longitude
        is_available               = [string]$row.is_available
        discounted_rate_zar        = [string]$row.discounted_rate_zar
        rate_card_zar              = [string]$row.rate_card_zar
        monthly_rate_zar           = [string]$row.monthly_rate_zar
        traffic_count              = [string]$row.traffic_count
        venue_type                 = [string]$row.venue_type
        premium_mass_fit           = [string]$row.premium_mass_fit
        price_positioning_fit      = [string]$row.price_positioning_fit
        audience_income_fit        = [string]$row.audience_income_fit
        youth_fit                  = [string]$row.youth_fit
        family_fit                 = [string]$row.family_fit
        professional_fit           = [string]$row.professional_fit
        commuter_fit               = [string]$row.commuter_fit
        tourist_fit                = [string]$row.tourist_fit
        high_value_shopper_fit     = [string]$row.high_value_shopper_fit
        audience_age_skew          = [string]$row.audience_age_skew
        audience_gender_skew       = [string]$row.audience_gender_skew
        dwell_time_score           = [string]$row.dwell_time_score
        environment_type           = [string]$row.environment_type
        buying_behaviour_fit       = [string]$row.buying_behaviour_fit
        primary_audience_tags_json = Convert-ToJsonArrayText ([string]$row.primary_audience_tags)
        secondary_audience_tags_json = Convert-ToJsonArrayText ([string]$row.secondary_audience_tags)
        recommendation_tags_json   = Convert-ToJsonArrayText ([string]$row.recommendation_tags)
        intelligence_notes         = [string]$row.intelligence_notes
        data_confidence            = [string]$row.data_confidence
        updated_by                 = [string]$row.updated_by
        metadata_json              = $metadataJson
    }
}

$stageRows | Export-Csv -LiteralPath $stageCsv -NoTypeInformation -Encoding UTF8

$sqlHeader = @"
begin;
$(if ($TruncateAll) {
"truncate table ooh_inventory_intelligence restart identity;"
} elseif (-not [string]::IsNullOrWhiteSpace($ReplaceSupplier)) {
"delete from ooh_inventory_intelligence where lower(coalesce(supplier, '')) = lower('$ReplaceSupplier');"
} else { "" })
drop table if exists tmp_ooh_inventory_intelligence_stage;
create temporary table tmp_ooh_inventory_intelligence_stage (
  supplier text,
  site_code text,
  site_name text,
  city text,
  suburb text,
  province text,
  media_type text,
  address text,
  latitude text,
  longitude text,
  is_available text,
  discounted_rate_zar text,
  rate_card_zar text,
  monthly_rate_zar text,
  traffic_count text,
  venue_type text,
  premium_mass_fit text,
  price_positioning_fit text,
  audience_income_fit text,
  youth_fit text,
  family_fit text,
  professional_fit text,
  commuter_fit text,
  tourist_fit text,
  high_value_shopper_fit text,
  audience_age_skew text,
  audience_gender_skew text,
  dwell_time_score text,
  environment_type text,
  buying_behaviour_fit text,
  primary_audience_tags_json text,
  secondary_audience_tags_json text,
  recommendation_tags_json text,
  intelligence_notes text,
  data_confidence text,
  updated_by text,
  metadata_json text
);
copy tmp_ooh_inventory_intelligence_stage (
  supplier,
  site_code,
  site_name,
  city,
  suburb,
  province,
  media_type,
  address,
  latitude,
  longitude,
  is_available,
  discounted_rate_zar,
  rate_card_zar,
  monthly_rate_zar,
  traffic_count,
  venue_type,
  premium_mass_fit,
  price_positioning_fit,
  audience_income_fit,
  youth_fit,
  family_fit,
  professional_fit,
  commuter_fit,
  tourist_fit,
  high_value_shopper_fit,
  audience_age_skew,
  audience_gender_skew,
  dwell_time_score,
  environment_type,
  buying_behaviour_fit,
  primary_audience_tags_json,
  secondary_audience_tags_json,
  recommendation_tags_json,
  intelligence_notes,
  data_confidence,
  updated_by,
  metadata_json
) from stdin with (format csv, header true);
"@

$sqlTail = @"
\.

drop table if exists tmp_ooh_inventory_intelligence_updated;
create temporary table tmp_ooh_inventory_intelligence_updated as
with updated as (
  update ooh_inventory_intelligence target
  set
    media_type = coalesce(nullif(stage.media_type, ''), target.media_type),
    address = coalesce(nullif(stage.address, ''), target.address),
    latitude = coalesce(nullif(stage.latitude, '')::double precision, target.latitude),
    longitude = coalesce(nullif(stage.longitude, '')::double precision, target.longitude),
    is_available = coalesce(nullif(stage.is_available, '')::boolean, target.is_available),
    discounted_rate_zar = coalesce(nullif(regexp_replace(coalesce(stage.discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2), target.discounted_rate_zar),
    rate_card_zar = coalesce(nullif(regexp_replace(coalesce(stage.rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2), target.rate_card_zar),
    monthly_rate_zar = coalesce(nullif(regexp_replace(coalesce(stage.monthly_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2), target.monthly_rate_zar),
    traffic_count = coalesce(nullif(regexp_replace(coalesce(stage.traffic_count, ''), '[^0-9]', '', 'g'), '')::bigint, target.traffic_count),
    venue_type = nullif(stage.venue_type, ''),
    premium_mass_fit = nullif(stage.premium_mass_fit, ''),
    price_positioning_fit = nullif(stage.price_positioning_fit, ''),
    audience_income_fit = nullif(stage.audience_income_fit, ''),
    youth_fit = nullif(stage.youth_fit, ''),
    family_fit = nullif(stage.family_fit, ''),
    professional_fit = nullif(stage.professional_fit, ''),
    commuter_fit = nullif(stage.commuter_fit, ''),
    tourist_fit = nullif(stage.tourist_fit, ''),
    high_value_shopper_fit = nullif(stage.high_value_shopper_fit, ''),
    audience_age_skew = nullif(stage.audience_age_skew, ''),
    audience_gender_skew = nullif(stage.audience_gender_skew, ''),
    dwell_time_score = nullif(stage.dwell_time_score, ''),
    environment_type = nullif(stage.environment_type, ''),
    buying_behaviour_fit = nullif(stage.buying_behaviour_fit, ''),
    primary_audience_tags_json = cast(stage.primary_audience_tags_json as jsonb),
    secondary_audience_tags_json = cast(stage.secondary_audience_tags_json as jsonb),
    recommendation_tags_json = cast(stage.recommendation_tags_json as jsonb),
    intelligence_notes = nullif(stage.intelligence_notes, ''),
    data_confidence = nullif(stage.data_confidence, ''),
    updated_by = nullif(stage.updated_by, ''),
    metadata_json = jsonb_strip_nulls(
      coalesce(target.metadata_json, '{}'::jsonb)
      || cast(stage.metadata_json as jsonb)
      || jsonb_build_object(
        'site_code', nullif(stage.site_code, ''),
        'media_type', nullif(stage.media_type, ''),
        'address', nullif(stage.address, ''),
        'latitude', nullif(stage.latitude, '')::double precision,
        'longitude', nullif(stage.longitude, '')::double precision,
        'discounted_rate_zar', nullif(regexp_replace(coalesce(stage.discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
        'rate_card_zar', nullif(regexp_replace(coalesce(stage.rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
        'monthly_rate_zar', nullif(regexp_replace(coalesce(stage.monthly_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
        'traffic_count', nullif(regexp_replace(coalesce(stage.traffic_count, ''), '[^0-9]', '', 'g'), '')::bigint,
        'available', nullif(stage.is_available, '')::boolean
      )
    ),
    updated_at = now(),
    is_active = true
  from tmp_ooh_inventory_intelligence_stage stage
  where lower(coalesce(target.supplier, '')) = lower(coalesce(stage.supplier, ''))
    and lower(coalesce(target.site_code, '')) = lower(coalesce(stage.site_code, ''))
    and lower(coalesce(target.site_name, '')) = lower(coalesce(stage.site_name, ''))
    and lower(coalesce(target.city, '')) = lower(coalesce(stage.city, ''))
    and lower(coalesce(target.suburb, '')) = lower(coalesce(stage.suburb, ''))
    and lower(coalesce(target.province, '')) = lower(coalesce(stage.province, ''))
  returning target.id
)
select * from updated;

drop table if exists tmp_ooh_inventory_intelligence_inserted;
create temporary table tmp_ooh_inventory_intelligence_inserted as
with inserted as (
  insert into ooh_inventory_intelligence (
    supplier,
    site_code,
    site_name,
    city,
    suburb,
    province,
    media_type,
    address,
    latitude,
    longitude,
    is_available,
    discounted_rate_zar,
    rate_card_zar,
    monthly_rate_zar,
    traffic_count,
    venue_type,
    premium_mass_fit,
    price_positioning_fit,
    audience_income_fit,
    youth_fit,
    family_fit,
    professional_fit,
    commuter_fit,
    tourist_fit,
    high_value_shopper_fit,
    audience_age_skew,
    audience_gender_skew,
    dwell_time_score,
    environment_type,
    buying_behaviour_fit,
    primary_audience_tags_json,
    secondary_audience_tags_json,
    recommendation_tags_json,
    intelligence_notes,
    data_confidence,
    updated_by,
    metadata_json,
    updated_at,
    is_active
  )
  select
    supplier,
    nullif(site_code, ''),
    site_name,
    nullif(city, ''),
    nullif(suburb, ''),
    nullif(province, ''),
    nullif(media_type, ''),
    nullif(address, ''),
    nullif(latitude, '')::double precision,
    nullif(longitude, '')::double precision,
    coalesce(nullif(is_available, '')::boolean, true),
    nullif(regexp_replace(coalesce(discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
    nullif(regexp_replace(coalesce(rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
    nullif(regexp_replace(coalesce(monthly_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
    nullif(regexp_replace(coalesce(traffic_count, ''), '[^0-9]', '', 'g'), '')::bigint,
    nullif(venue_type, ''),
    nullif(premium_mass_fit, ''),
    nullif(price_positioning_fit, ''),
    nullif(audience_income_fit, ''),
    nullif(youth_fit, ''),
    nullif(family_fit, ''),
    nullif(professional_fit, ''),
    nullif(commuter_fit, ''),
    nullif(tourist_fit, ''),
    nullif(high_value_shopper_fit, ''),
    nullif(audience_age_skew, ''),
    nullif(audience_gender_skew, ''),
    nullif(dwell_time_score, ''),
    nullif(environment_type, ''),
    nullif(buying_behaviour_fit, ''),
    cast(primary_audience_tags_json as jsonb),
    cast(secondary_audience_tags_json as jsonb),
    cast(recommendation_tags_json as jsonb),
    nullif(intelligence_notes, ''),
    nullif(data_confidence, ''),
    nullif(updated_by, ''),
    jsonb_strip_nulls(
      cast(metadata_json as jsonb)
      || jsonb_build_object(
        'site_code', nullif(site_code, ''),
        'media_type', nullif(media_type, ''),
        'address', nullif(address, ''),
        'latitude', nullif(latitude, '')::double precision,
        'longitude', nullif(longitude, '')::double precision,
        'discounted_rate_zar', nullif(regexp_replace(coalesce(discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
        'rate_card_zar', nullif(regexp_replace(coalesce(rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
        'monthly_rate_zar', nullif(regexp_replace(coalesce(monthly_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric(18,2),
        'traffic_count', nullif(regexp_replace(coalesce(traffic_count, ''), '[^0-9]', '', 'g'), '')::bigint,
        'available', coalesce(nullif(is_available, '')::boolean, true)
      )
    ),
    now(),
    true
  from tmp_ooh_inventory_intelligence_stage
  where not exists (
    select 1
    from ooh_inventory_intelligence existing
    where lower(coalesce(existing.supplier, '')) = lower(coalesce(tmp_ooh_inventory_intelligence_stage.supplier, ''))
      and lower(coalesce(existing.site_code, '')) = lower(coalesce(tmp_ooh_inventory_intelligence_stage.site_code, ''))
      and lower(coalesce(existing.site_name, '')) = lower(coalesce(tmp_ooh_inventory_intelligence_stage.site_name, ''))
      and lower(coalesce(existing.city, '')) = lower(coalesce(tmp_ooh_inventory_intelligence_stage.city, ''))
      and lower(coalesce(existing.suburb, '')) = lower(coalesce(tmp_ooh_inventory_intelligence_stage.suburb, ''))
      and lower(coalesce(existing.province, '')) = lower(coalesce(tmp_ooh_inventory_intelligence_stage.province, ''))
  )
  returning id
)
select * from inserted;

select
  (select count(*) from tmp_ooh_inventory_intelligence_updated) as updated_rows,
  (select count(*) from tmp_ooh_inventory_intelligence_inserted) as inserted_rows,
  (select count(*) from ooh_inventory_intelligence where is_active = true) as active_rows;

commit;
"@

$sqlPath = Join-Path $tmpDirectory "ooh_inventory_intelligence_import.sql"
Set-Content -LiteralPath $sqlPath -Value (($sqlHeader -split "`r?`n") + (Get-Content -LiteralPath $stageCsv) + ($sqlTail -split "`r?`n"))

Get-Content -LiteralPath $sqlPath | ssh -i $SshKeyPath "$SshUser@$SshHost" "docker exec -i $DbContainer psql -U $DbUser -d $Database"

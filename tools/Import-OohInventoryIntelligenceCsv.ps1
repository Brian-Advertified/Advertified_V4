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
    [string]$DbUser = "advertified"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CsvPath)) {
    throw "CSV was not found: $CsvPath"
}

$rows = Import-Csv -LiteralPath $CsvPath
if ($rows.Count -eq 0) {
    throw "CSV contains no rows: $CsvPath"
}

function Convert-ToJsonArrayText {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "[]"
    }

    $items = $Value.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne "" }

    return ($items | ConvertTo-Json -Compress)
}

$tmpDirectory = Join-Path $PSScriptRoot "..\.tmp"
New-Item -ItemType Directory -Force -Path $tmpDirectory | Out-Null
$stageCsv = Join-Path $tmpDirectory "ooh_inventory_intelligence_stage.csv"

$stageRows = foreach ($row in $rows) {
    $metadata = [ordered]@{}
    foreach ($property in $row.PSObject.Properties) {
        if ($property.Name -in @(
            "supplier", "site_code", "site_name", "city", "suburb", "province", "inventory_rows",
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
drop table if exists tmp_ooh_inventory_intelligence_stage;
create temporary table tmp_ooh_inventory_intelligence_stage (
  supplier text,
  site_code text,
  site_name text,
  city text,
  suburb text,
  province text,
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
    metadata_json = cast(stage.metadata_json as jsonb),
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
    cast(metadata_json as jsonb),
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

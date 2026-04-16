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
$stageCsv = Join-Path $tmpDirectory "radio_inventory_intelligence_stage.csv"

$knownColumns = @(
    "station_name","row_kind","slot","day_group","package_name","broadcast_frequency","coverage_type",
    "province_codes","city_labels","language_codes","target_audience","existing_audience_age_skew",
    "existing_audience_gender_skew","existing_audience_lsm_range","station_tier","station_format",
    "audience_income_fit","premium_mass_fit","price_positioning_fit","youth_fit","family_fit",
    "professional_fit","commuter_fit","high_value_client_fit","business_decision_maker_fit",
    "morning_drive_fit","workday_fit","afternoon_drive_fit","evening_fit","weekend_fit","urban_rural_fit",
    "language_context_fit","buying_behaviour_fit","brand_safety_fit","objective_fit_primary",
    "objective_fit_secondary","audience_age_skew","audience_gender_skew","content_environment",
    "presenter_or_show_context","primary_audience_tags","secondary_audience_tags","recommendation_tags",
    "intelligence_notes","source_urls","data_confidence","updated_by","internal_key","media_outlet_code",
    "source_type","genre_fit","household_decision_maker_fit","source_file"
)

$stageRows = foreach ($row in $rows) {
    $metadata = [ordered]@{}
    foreach ($property in $row.PSObject.Properties) {
        if ($property.Name -in $knownColumns) {
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
        media_outlet_code             = [string]$row.media_outlet_code
        station_name                  = [string]$row.station_name
        inventory_scope               = if ([string]::IsNullOrWhiteSpace([string]$row.row_kind)) { "slot" } else { [string]$row.row_kind }
        source_type                   = [string]$row.source_type
        internal_key                  = [string]$row.internal_key
        slot_label                    = [string]$row.slot
        day_group                     = [string]$row.day_group
        package_name                  = [string]$row.package_name
        broadcast_frequency           = [string]$row.broadcast_frequency
        coverage_type                 = [string]$row.coverage_type
        province_codes_json           = Convert-ToJsonArrayText ([string]$row.province_codes)
        city_labels_json              = Convert-ToJsonArrayText ([string]$row.city_labels)
        language_codes_json           = Convert-ToJsonArrayText ([string]$row.language_codes)
        station_tier                  = [string]$row.station_tier
        station_format                = [string]$row.station_format
        audience_income_fit           = [string]$row.audience_income_fit
        premium_mass_fit              = [string]$row.premium_mass_fit
        price_positioning_fit         = [string]$row.price_positioning_fit
        youth_fit                     = [string]$row.youth_fit
        family_fit                    = [string]$row.family_fit
        professional_fit              = [string]$row.professional_fit
        commuter_fit                  = [string]$row.commuter_fit
        high_value_client_fit         = [string]$row.high_value_client_fit
        business_decision_maker_fit   = [string]$row.business_decision_maker_fit
        household_decision_maker_fit  = [string]$row.household_decision_maker_fit
        morning_drive_fit             = [string]$row.morning_drive_fit
        workday_fit                   = [string]$row.workday_fit
        afternoon_drive_fit           = [string]$row.afternoon_drive_fit
        evening_fit                   = [string]$row.evening_fit
        weekend_fit                   = [string]$row.weekend_fit
        urban_rural_fit               = [string]$row.urban_rural_fit
        language_context_fit          = [string]$row.language_context_fit
        buying_behaviour_fit          = [string]$row.buying_behaviour_fit
        brand_safety_fit              = [string]$row.brand_safety_fit
        objective_fit_primary         = [string]$row.objective_fit_primary
        objective_fit_secondary       = [string]$row.objective_fit_secondary
        audience_age_skew             = [string]$row.audience_age_skew
        audience_gender_skew          = [string]$row.audience_gender_skew
        content_environment           = [string]$row.content_environment
        presenter_or_show_context     = [string]$row.presenter_or_show_context
        genre_fit                     = [string]$row.genre_fit
        primary_audience_tags_json    = Convert-ToJsonArrayText ([string]$row.primary_audience_tags)
        secondary_audience_tags_json  = Convert-ToJsonArrayText ([string]$row.secondary_audience_tags)
        recommendation_tags_json      = Convert-ToJsonArrayText ([string]$row.recommendation_tags)
        intelligence_notes            = [string]$row.intelligence_notes
        source_urls_json              = Convert-ToJsonArrayText ([string]$row.source_urls)
        data_confidence               = [string]$row.data_confidence
        updated_by                    = [string]$row.updated_by
        source_file                   = [string]$row.source_file
        metadata_json                 = $metadataJson
    }
}

$stageRows | Export-Csv -LiteralPath $stageCsv -NoTypeInformation -Encoding UTF8

$sqlHeader = @"
begin;
drop table if exists tmp_radio_inventory_intelligence_stage;
create temporary table tmp_radio_inventory_intelligence_stage (
  media_outlet_code text,
  station_name text,
  inventory_scope text,
  source_type text,
  internal_key text,
  slot_label text,
  day_group text,
  package_name text,
  broadcast_frequency text,
  coverage_type text,
  province_codes_json text,
  city_labels_json text,
  language_codes_json text,
  station_tier text,
  station_format text,
  audience_income_fit text,
  premium_mass_fit text,
  price_positioning_fit text,
  youth_fit text,
  family_fit text,
  professional_fit text,
  commuter_fit text,
  high_value_client_fit text,
  business_decision_maker_fit text,
  household_decision_maker_fit text,
  morning_drive_fit text,
  workday_fit text,
  afternoon_drive_fit text,
  evening_fit text,
  weekend_fit text,
  urban_rural_fit text,
  language_context_fit text,
  buying_behaviour_fit text,
  brand_safety_fit text,
  objective_fit_primary text,
  objective_fit_secondary text,
  audience_age_skew text,
  audience_gender_skew text,
  content_environment text,
  presenter_or_show_context text,
  genre_fit text,
  primary_audience_tags_json text,
  secondary_audience_tags_json text,
  recommendation_tags_json text,
  intelligence_notes text,
  source_urls_json text,
  data_confidence text,
  updated_by text,
  source_file text,
  metadata_json text
);
copy tmp_radio_inventory_intelligence_stage from stdin with (format csv, header true);
"@

$sqlTail = @"
\.

update radio_inventory_intelligence target
set
  media_outlet_code = stage.media_outlet_code,
  station_name = stage.station_name,
  inventory_scope = nullif(stage.inventory_scope, ''),
  source_type = nullif(stage.source_type, ''),
  slot_label = nullif(stage.slot_label, ''),
  day_group = nullif(stage.day_group, ''),
  package_name = nullif(stage.package_name, ''),
  broadcast_frequency = nullif(stage.broadcast_frequency, ''),
  coverage_type = nullif(stage.coverage_type, ''),
  province_codes_json = cast(stage.province_codes_json as jsonb),
  city_labels_json = cast(stage.city_labels_json as jsonb),
  language_codes_json = cast(stage.language_codes_json as jsonb),
  station_tier = nullif(stage.station_tier, ''),
  station_format = nullif(stage.station_format, ''),
  audience_income_fit = nullif(stage.audience_income_fit, ''),
  premium_mass_fit = nullif(stage.premium_mass_fit, ''),
  price_positioning_fit = nullif(stage.price_positioning_fit, ''),
  youth_fit = nullif(stage.youth_fit, ''),
  family_fit = nullif(stage.family_fit, ''),
  professional_fit = nullif(stage.professional_fit, ''),
  commuter_fit = nullif(stage.commuter_fit, ''),
  high_value_client_fit = nullif(stage.high_value_client_fit, ''),
  business_decision_maker_fit = nullif(stage.business_decision_maker_fit, ''),
  household_decision_maker_fit = nullif(stage.household_decision_maker_fit, ''),
  morning_drive_fit = nullif(stage.morning_drive_fit, ''),
  workday_fit = nullif(stage.workday_fit, ''),
  afternoon_drive_fit = nullif(stage.afternoon_drive_fit, ''),
  evening_fit = nullif(stage.evening_fit, ''),
  weekend_fit = nullif(stage.weekend_fit, ''),
  urban_rural_fit = nullif(stage.urban_rural_fit, ''),
  language_context_fit = nullif(stage.language_context_fit, ''),
  buying_behaviour_fit = nullif(stage.buying_behaviour_fit, ''),
  brand_safety_fit = nullif(stage.brand_safety_fit, ''),
  objective_fit_primary = nullif(stage.objective_fit_primary, ''),
  objective_fit_secondary = nullif(stage.objective_fit_secondary, ''),
  audience_age_skew = nullif(stage.audience_age_skew, ''),
  audience_gender_skew = nullif(stage.audience_gender_skew, ''),
  content_environment = nullif(stage.content_environment, ''),
  presenter_or_show_context = nullif(stage.presenter_or_show_context, ''),
  genre_fit = nullif(stage.genre_fit, ''),
  primary_audience_tags_json = cast(stage.primary_audience_tags_json as jsonb),
  secondary_audience_tags_json = cast(stage.secondary_audience_tags_json as jsonb),
  recommendation_tags_json = cast(stage.recommendation_tags_json as jsonb),
  intelligence_notes = nullif(stage.intelligence_notes, ''),
  source_urls_json = cast(stage.source_urls_json as jsonb),
  data_confidence = nullif(stage.data_confidence, ''),
  updated_by = nullif(stage.updated_by, ''),
  source_file = nullif(stage.source_file, ''),
  metadata_json = cast(stage.metadata_json as jsonb),
  updated_at = now(),
  is_active = true
from tmp_radio_inventory_intelligence_stage stage
where lower(target.internal_key) = lower(stage.internal_key);

insert into radio_inventory_intelligence (
  media_outlet_code, station_name, inventory_scope, source_type, internal_key, slot_label, day_group, package_name,
  broadcast_frequency, coverage_type, province_codes_json, city_labels_json, language_codes_json, station_tier,
  station_format, audience_income_fit, premium_mass_fit, price_positioning_fit, youth_fit, family_fit,
  professional_fit, commuter_fit, high_value_client_fit, business_decision_maker_fit, household_decision_maker_fit,
  morning_drive_fit, workday_fit, afternoon_drive_fit, evening_fit, weekend_fit, urban_rural_fit,
  language_context_fit, buying_behaviour_fit, brand_safety_fit, objective_fit_primary, objective_fit_secondary,
  audience_age_skew, audience_gender_skew, content_environment, presenter_or_show_context, genre_fit,
  primary_audience_tags_json, secondary_audience_tags_json, recommendation_tags_json, intelligence_notes,
  source_urls_json, data_confidence, updated_by, source_file, metadata_json, is_active, updated_at
)
select
  media_outlet_code, station_name, nullif(inventory_scope, ''), nullif(source_type, ''), internal_key, nullif(slot_label, ''),
  nullif(day_group, ''), nullif(package_name, ''), nullif(broadcast_frequency, ''), nullif(coverage_type, ''),
  cast(province_codes_json as jsonb), cast(city_labels_json as jsonb), cast(language_codes_json as jsonb),
  nullif(station_tier, ''), nullif(station_format, ''), nullif(audience_income_fit, ''), nullif(premium_mass_fit, ''),
  nullif(price_positioning_fit, ''), nullif(youth_fit, ''), nullif(family_fit, ''), nullif(professional_fit, ''),
  nullif(commuter_fit, ''), nullif(high_value_client_fit, ''), nullif(business_decision_maker_fit, ''),
  nullif(household_decision_maker_fit, ''), nullif(morning_drive_fit, ''), nullif(workday_fit, ''),
  nullif(afternoon_drive_fit, ''), nullif(evening_fit, ''), nullif(weekend_fit, ''), nullif(urban_rural_fit, ''),
  nullif(language_context_fit, ''), nullif(buying_behaviour_fit, ''), nullif(brand_safety_fit, ''),
  nullif(objective_fit_primary, ''), nullif(objective_fit_secondary, ''), nullif(audience_age_skew, ''),
  nullif(audience_gender_skew, ''), nullif(content_environment, ''), nullif(presenter_or_show_context, ''),
  nullif(genre_fit, ''), cast(primary_audience_tags_json as jsonb), cast(secondary_audience_tags_json as jsonb),
  cast(recommendation_tags_json as jsonb), nullif(intelligence_notes, ''), cast(source_urls_json as jsonb),
  nullif(data_confidence, ''), nullif(updated_by, ''), nullif(source_file, ''), cast(metadata_json as jsonb), true, now()
from tmp_radio_inventory_intelligence_stage stage
where not exists (
  select 1
  from radio_inventory_intelligence existing
  where lower(existing.internal_key) = lower(stage.internal_key)
);

select count(*) as active_rows
from radio_inventory_intelligence
where is_active = true;

commit;
"@

$sqlPath = Join-Path $tmpDirectory "radio_inventory_intelligence_import.sql"
Set-Content -LiteralPath $sqlPath -Value (($sqlHeader -split "`r?`n") + (Get-Content -LiteralPath $stageCsv) + ($sqlTail -split "`r?`n"))

Get-Content -LiteralPath $sqlPath | ssh -i $SshKeyPath "$SshUser@$SshHost" "docker exec -i $DbContainer psql -U $DbUser -d $Database"

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
    [switch]$PreviewOnly
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CsvPath)) {
    throw "Correction CSV was not found: $CsvPath"
}

if (-not (Test-Path -LiteralPath $SshKeyPath)) {
    throw "SSH key was not found: $SshKeyPath"
}

$rows = Import-Csv -LiteralPath $CsvPath | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.corrected_site_name) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_media_type) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_city) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_suburb) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_province) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_site_code) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_discounted_rate_zar) -or
    -not [string]::IsNullOrWhiteSpace($_.corrected_rate_card_zar)
}

if ($rows.Count -eq 0) {
    throw "Correction CSV contains no populated corrections: $CsvPath"
}

$tmpDirectory = Join-Path $PSScriptRoot "..\.tmp"
New-Item -ItemType Directory -Force -Path $tmpDirectory | Out-Null
$stageCsv = Join-Path $tmpDirectory "ooh_price_corrections_stage.csv"
$rows | Export-Csv -LiteralPath $stageCsv -NoTypeInformation -Encoding UTF8

$sqlHeader = @"
begin;
drop table if exists tmp_ooh_price_corrections;
create temporary table tmp_ooh_price_corrections (
  supplier text,
  source_file text,
  current_site_name text,
  current_media_type text,
  current_city text,
  current_suburb text,
  current_province text,
  current_site_code text,
  current_discounted_rate_zar text,
  current_rate_card_zar text,
  flags text,
  corrected_site_name text,
  corrected_media_type text,
  corrected_city text,
  corrected_suburb text,
  corrected_province text,
  corrected_site_code text,
  corrected_discounted_rate_zar text,
  corrected_rate_card_zar text,
  correction_notes text,
  verified_source text,
  verified_by text
);
copy tmp_ooh_price_corrections (
  supplier,
  source_file,
  current_site_name,
  current_media_type,
  current_city,
  current_suburb,
  current_province,
  current_site_code,
  current_discounted_rate_zar,
  current_rate_card_zar,
  flags,
  corrected_site_name,
  corrected_media_type,
  corrected_city,
  corrected_suburb,
  corrected_province,
  corrected_site_code,
  corrected_discounted_rate_zar,
  corrected_rate_card_zar,
  correction_notes,
  verified_source,
  verified_by
) from stdin with (format csv, header true);
"@

$sqlTail = @"
\.

drop table if exists tmp_ooh_price_corrections_prepared;
create temporary table tmp_ooh_price_corrections_prepared as
select
  row_number() over (
    partition by
      lower(trim(coalesce(supplier, ''))),
      lower(trim(coalesce(current_site_name, ''))),
      lower(trim(coalesce(current_media_type, ''))),
      lower(trim(coalesce(current_city, ''))),
      lower(trim(coalesce(current_suburb, ''))),
      lower(trim(coalesce(current_province, ''))),
      nullif(regexp_replace(coalesce(current_discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric,
      nullif(regexp_replace(coalesce(current_rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric
    order by
      lower(trim(coalesce(corrected_site_code, ''))),
      lower(trim(coalesce(corrected_site_name, ''))),
      lower(trim(coalesce(correction_notes, '')))
  ) as match_ordinal,
  supplier,
  source_file,
  current_site_name,
  current_media_type,
  current_city,
  current_suburb,
  current_province,
  current_site_code,
  nullif(regexp_replace(coalesce(current_discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric as current_discounted_rate_zar,
  nullif(regexp_replace(coalesce(current_rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric as current_rate_card_zar,
  flags,
  nullif(corrected_site_name, '') as corrected_site_name,
  nullif(corrected_media_type, '') as corrected_media_type,
  nullif(corrected_city, '') as corrected_city,
  nullif(corrected_suburb, '') as corrected_suburb,
  nullif(corrected_province, '') as corrected_province,
  nullif(corrected_site_code, '') as corrected_site_code,
  nullif(regexp_replace(coalesce(corrected_discounted_rate_zar, ''), '[^0-9.]', '', 'g'), '')::numeric as corrected_discounted_rate_zar,
  nullif(regexp_replace(coalesce(corrected_rate_card_zar, ''), '[^0-9.]', '', 'g'), '')::numeric as corrected_rate_card_zar,
  nullif(correction_notes, '') as correction_notes,
  nullif(verified_source, '') as verified_source,
  nullif(verified_by, '') as verified_by
from tmp_ooh_price_corrections;

drop table if exists tmp_ooh_price_corrections_inventory_candidates;
create temporary table tmp_ooh_price_corrections_inventory_candidates as
select
  row_number() over (
    partition by
      lower(trim(coalesce(tgt.supplier, ''))),
      lower(trim(coalesce(tgt.site_name, ''))),
      lower(trim(coalesce(tgt.media_type, ''))),
      lower(trim(coalesce(tgt.city, ''))),
      lower(trim(coalesce(tgt.suburb, ''))),
      lower(trim(coalesce(tgt.province, ''))),
      coalesce((tgt.metadata_json ->> 'discounted_rate_zar')::numeric, -1),
      coalesce((tgt.metadata_json ->> 'rate_card_zar')::numeric, -1)
    order by tgt.id
  ) as match_ordinal,
  tgt.id,
  tgt.supplier,
  tgt.site_name,
  tgt.media_type,
  tgt.city,
  tgt.suburb,
  tgt.province,
  coalesce(tgt.metadata_json ->> 'site_code', '') as site_code,
  coalesce((tgt.metadata_json ->> 'discounted_rate_zar')::numeric, -1) as discounted_rate_zar,
  coalesce((tgt.metadata_json ->> 'rate_card_zar')::numeric, -1) as rate_card_zar
from inventory_items_final tgt;

drop table if exists tmp_ooh_price_corrections_matches;
create temporary table tmp_ooh_price_corrections_matches as
select
  tgt.id,
  stage.*
from tmp_ooh_price_corrections_inventory_candidates tgt
join tmp_ooh_price_corrections_prepared stage
  on lower(trim(coalesce(tgt.supplier, ''))) = lower(trim(coalesce(stage.supplier, '')))
 and lower(trim(coalesce(tgt.site_name, ''))) = lower(trim(coalesce(stage.current_site_name, '')))
 and lower(trim(coalesce(tgt.media_type, ''))) = lower(trim(coalesce(stage.current_media_type, '')))
 and lower(trim(coalesce(tgt.city, ''))) = lower(trim(coalesce(stage.current_city, '')))
 and lower(trim(coalesce(tgt.suburb, ''))) = lower(trim(coalesce(stage.current_suburb, '')))
 and lower(trim(coalesce(tgt.province, ''))) = lower(trim(coalesce(stage.current_province, '')))
 and tgt.discounted_rate_zar = coalesce(stage.current_discounted_rate_zar, -1)
 and tgt.rate_card_zar = coalesce(stage.current_rate_card_zar, -1)
 and tgt.match_ordinal = stage.match_ordinal;

select 'staged_corrections' as metric, count(*)::text as value from tmp_ooh_price_corrections_prepared
union all
select 'matched_inventory_rows', count(*)::text from tmp_ooh_price_corrections_matches;
"@

if ($PreviewOnly) {
    $sqlTail += @"

rollback;
"@
}
else {
    $sqlTail += @"

drop table if exists tmp_ooh_price_corrections_updated;
create temporary table tmp_ooh_price_corrections_updated as
with updated as (
  update inventory_items_final tgt
  set
    site_name = coalesce(src.corrected_site_name, tgt.site_name),
    media_type = coalesce(src.corrected_media_type, tgt.media_type),
    city = coalesce(src.corrected_city, tgt.city),
    suburb = coalesce(src.corrected_suburb, tgt.suburb),
    province = coalesce(src.corrected_province, tgt.province),
    address = concat_ws(', ',
      coalesce(src.corrected_site_name, tgt.site_name),
      coalesce(src.corrected_suburb, tgt.suburb),
      coalesce(src.corrected_province, tgt.province)),
    metadata_json = jsonb_strip_nulls(
      tgt.metadata_json
      || case when src.corrected_site_code is not null then jsonb_build_object('site_code', src.corrected_site_code) else '{}'::jsonb end
      || case when src.corrected_discounted_rate_zar is not null then jsonb_build_object('discounted_rate_zar', src.corrected_discounted_rate_zar) else '{}'::jsonb end
      || case when src.corrected_rate_card_zar is not null then jsonb_build_object('rate_card_zar', src.corrected_rate_card_zar) else '{}'::jsonb end
      || jsonb_build_object(
          'price_correction_notes', src.correction_notes,
          'price_correction_verified_source', src.verified_source,
          'price_correction_verified_by', src.verified_by,
          'price_correction_applied_at', now()::text
      )
    )
  from tmp_ooh_price_corrections_matches src
  where tgt.id = src.id
  returning tgt.id
)
select * from updated;

select 'updated_rows', count(*)::text from tmp_ooh_price_corrections_updated;

commit;
"@
}

$sqlPath = Join-Path $tmpDirectory "ooh_price_corrections_apply.sql"
Set-Content -LiteralPath $sqlPath -Value (($sqlHeader -split "`r?`n") + (Get-Content -LiteralPath $stageCsv) + ($sqlTail -split "`r?`n"))

Get-Content -LiteralPath $sqlPath | ssh -i $SshKeyPath "$SshUser@$SshHost" "docker exec -i $DbContainer psql -U $DbUser -d $Database"

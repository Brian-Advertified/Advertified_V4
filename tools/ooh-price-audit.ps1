param(
    [string]$SshKeyPath = "C:\Users\CC KEMPTON\Downloads\advertified.pem",
    [string]$SshHost = "ubuntu@13.246.60.13",
    [string]$RemoteAppPath = "/home/ubuntu/apps/advertified-v4-dev",
    [string]$DatabaseContainer = "advertified-v4-dev-db-1",
    [string]$DatabaseName = "advertified_v4_dev",
    [string]$DatabaseUser = "advertified",
    [string]$OutputPath = ".exports/ooh-price-audit.csv"
)

$ErrorActionPreference = "Stop"

$sql = @"
with inventory as (
    select
        id,
        supplier,
        coalesce(metadata_json->>'source_file', '') as source_file,
        coalesce(site_name, '') as site_name,
        coalesce(media_type, '') as media_type,
        coalesce(city, '') as city,
        coalesce(suburb, '') as suburb,
        coalesce(province, '') as province,
        coalesce(metadata_json->>'site_code', '') as site_code,
        nullif(regexp_replace(coalesce(metadata_json->>'discounted_rate_zar', ''), '[^0-9.]', '', 'g'), '')::numeric as discounted_rate_zar,
        nullif(regexp_replace(coalesce(metadata_json->>'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric as rate_card_zar,
        nullif(regexp_replace(coalesce(metadata_json->>'monthly_rate_zar', ''), '[^0-9.]', '', 'g'), '')::numeric as monthly_rate_zar,
        regexp_replace(lower(coalesce(site_name, '')), '[^a-z0-9]+', '', 'g') as normalized_site_name
    from inventory_items_final
),
ranked as (
    select
        inventory.*,
        count(*) over (partition by normalized_site_name) as normalized_site_count,
        count(*) over (partition by normalized_site_name, lower(media_type)) as normalized_site_media_count,
        min(discounted_rate_zar) over (partition by normalized_site_name) as min_site_discounted_rate,
        max(discounted_rate_zar) over (partition by normalized_site_name) as max_site_discounted_rate,
        row_number() over (partition by normalized_site_name, lower(media_type), discounted_rate_zar order by site_name, id) as duplicate_price_rank
    from inventory
),
flagged as (
    select
        *,
        array_remove(array[
            case when coalesce(discounted_rate_zar, 0) <= 0 and coalesce(rate_card_zar, 0) <= 0 and coalesce(monthly_rate_zar, 0) <= 0 then 'missing_price' end,
            case when discounted_rate_zar is not null and rate_card_zar is not null and discounted_rate_zar > rate_card_zar then 'discount_above_rate_card' end,
            case when rate_card_zar is not null and discounted_rate_zar is not null and rate_card_zar > 0 and discounted_rate_zar / rate_card_zar < 0.45 then 'heavy_discount_check' end,
            case when normalized_site_media_count > 1 and duplicate_price_rank > 1 then 'possible_duplicate_row' end,
            case when normalized_site_count > 1 and max_site_discounted_rate is not null and min_site_discounted_rate is not null and max_site_discounted_rate >= greatest(min_site_discounted_rate * 4, min_site_discounted_rate + 30000) then 'wide_site_price_range_review' end,
            case when site_code = '' then 'missing_site_code' end,
            case when province <> '' and province not in ('Eastern Cape','Free State','Gauteng','KwaZulu-Natal','Limpopo','Mpumalanga','North West','Northern Cape','Western Cape') then 'province_parse_check' end,
            case when media_type = '' then 'missing_media_type' end
        ], null) as flags
    from ranked
)
select
    supplier,
    source_file,
    site_name,
    media_type,
    city,
    suburb,
    province,
    site_code,
    discounted_rate_zar,
    rate_card_zar,
    monthly_rate_zar,
    normalized_site_count,
    normalized_site_media_count,
    min_site_discounted_rate,
    max_site_discounted_rate,
    array_to_string(flags, ';') as flags
from flagged
where cardinality(flags) > 0
order by source_file, supplier, site_name, media_type, discounted_rate_zar desc nulls last;
"@

$remoteCommand = @"
cd $RemoteAppPath
cat <<'SQL' | docker exec -i $DatabaseContainer psql -U $DatabaseUser -d $DatabaseName -P footer=off -F ',' --csv
$sql
SQL
"@

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$csv = ssh -i $SshKeyPath $SshHost $remoteCommand
Set-Content -Path $OutputPath -Value $csv -Encoding UTF8

$rows = Import-Csv -Path $OutputPath
$flagCounts = $rows |
    ForEach-Object { ($_.flags -split ';' | Where-Object { $_ -ne '' }) } |
    Group-Object |
    Sort-Object Count -Descending

Write-Host "Wrote audit to $OutputPath"
Write-Host ""
Write-Host "Flag counts:"
$flagCounts | ForEach-Object {
    Write-Host ("- {0}: {1}" -f $_.Name, $_.Count)
}

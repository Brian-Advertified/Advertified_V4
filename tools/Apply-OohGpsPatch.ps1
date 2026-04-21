[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$JsonPath,

    [string]$SshKeyPath,

    [string]$SshHost,

    [string]$SshUser = "ubuntu",

    [string]$RemoteAppPath = "/home/ubuntu/apps/advertified-v4-dev",

    [string]$DbContainer = "advertified-v4-dev-db-1",

    [string]$DatabaseName = "advertified_v4_dev",

    [string]$DatabaseUser = "advertified",

    [switch]$WriteLocalSqlOnly,

    [string]$OutputSqlPath = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $JsonPath)) {
    throw "JSON patch file not found: $JsonPath"
}

$json = Get-Content -LiteralPath $JsonPath -Raw | ConvertFrom-Json
if (-not $json) {
    throw "JSON patch file did not contain any records."
}

function Escape-SqlLiteral {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return "null"
    }

    return "'" + $Value.Replace("'", "''") + "'"
}

$values = foreach ($row in $json) {
    if ([string]::IsNullOrWhiteSpace($row.siteNumber)) {
        continue
    }

    $siteNumber = Escape-SqlLiteral ([string]$row.siteNumber)
    $title = Escape-SqlLiteral ([string]$row.title)
    $gpsCoordinatesText = Escape-SqlLiteral ([string]$row.gpsCoordinatesText)
    $latitude = if ($null -ne $row.latitude) { [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0}", [double]$row.latitude) } else { "null" }
    $longitude = if ($null -ne $row.longitude) { [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0}", [double]$row.longitude) } else { "null" }

    "    ($siteNumber, $title, $latitude, $longitude, $gpsCoordinatesText)"
}

if (-not $values) {
    throw "No valid rows with siteNumber were found in the patch file."
}

$sql = @"
with patch(site_code, title, latitude, longitude, gps_coordinates_text) as (
values
$(($values -join ",`n"))
)
update inventory_items_final iif
set latitude = patch.latitude,
    longitude = patch.longitude,
    metadata_json = jsonb_strip_nulls(
        coalesce(iif.metadata_json, '{}'::jsonb)
        || jsonb_build_object(
            'gps_coordinates', patch.gps_coordinates_text,
            'latitude', patch.latitude,
            'longitude', patch.longitude
        )
    )
from patch
where lower(coalesce(iif.metadata_json ->> 'site_code', '')) = lower(patch.site_code);

select
    patch.site_code,
    patch.title,
    iif.id as inventory_id,
    iif.site_name,
    iif.latitude,
    iif.longitude
from patch
left join inventory_items_final iif
    on lower(coalesce(iif.metadata_json ->> 'site_code', '')) = lower(patch.site_code)
order by patch.site_code;
"@

if ([string]::IsNullOrWhiteSpace($OutputSqlPath)) {
    $OutputSqlPath = Join-Path -Path ([System.IO.Path]::GetDirectoryName($JsonPath)) -ChildPath "apply-ooh-gps-patch.sql"
}

Set-Content -LiteralPath $OutputSqlPath -Value $sql -Encoding UTF8
Write-Host "Wrote SQL patch to $OutputSqlPath"

if ($WriteLocalSqlOnly) {
    return
}

if ([string]::IsNullOrWhiteSpace($SshKeyPath) -or [string]::IsNullOrWhiteSpace($SshHost)) {
    throw "SshKeyPath and SshHost are required unless -WriteLocalSqlOnly is used."
}

if (-not (Test-Path -LiteralPath $SshKeyPath)) {
    throw "SSH key was not found: $SshKeyPath"
}

Get-Content -LiteralPath $OutputSqlPath | ssh -i $SshKeyPath "$SshUser@$SshHost" "docker exec -i $DbContainer psql -U $DatabaseUser -d $DatabaseName"

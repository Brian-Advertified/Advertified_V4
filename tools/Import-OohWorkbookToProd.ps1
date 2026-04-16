param(
    [Parameter(Mandatory = $true)]
    [string]$WorkbookPath,

    [Parameter(Mandatory = $true)]
    [string]$SshKeyPath,

    [Parameter(Mandatory = $true)]
    [string]$SshHost,

    [string]$SshUser = "ubuntu",
    [string]$DbContainer = "advertified-v4-prod-db-1",
    [string]$Database = "advertified_v4_prod",
    [string]$DbUser = "advertified",
    [string]$Supplier = "Eleven8",
    [string]$SourceFileName = "",
    [string]$InventorySource = "",
    [switch]$SkipExistingSites = $true
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $WorkbookPath)) {
    throw "Workbook was not found: $WorkbookPath"
}

if (-not (Test-Path -LiteralPath $SshKeyPath)) {
    throw "SSH key was not found: $SshKeyPath"
}

if ([string]::IsNullOrWhiteSpace($SourceFileName)) {
    $SourceFileName = [System.IO.Path]::GetFileName($WorkbookPath)
}

if ([string]::IsNullOrWhiteSpace($InventorySource)) {
    $InventorySource = ([System.IO.Path]::GetFileNameWithoutExtension($WorkbookPath) -replace "[^A-Za-z0-9]+", "_").ToLowerInvariant()
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Parse-OohWorkbook {
    param(
        [string]$Path,
        [string]$SupplierName
    )

    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $sharedStringsXml = (New-Object IO.StreamReader($zip.GetEntry("xl/sharedStrings.xml").Open())).ReadToEnd()
        $sharedStrings = @()
        foreach ($stringMatch in [regex]::Matches($sharedStringsXml, "<si(?:.|\n|\r)*?</si>")) {
            $parts = [regex]::Matches($stringMatch.Value, "<t[^>]*>(.*?)</t>") | ForEach-Object {
                [System.Net.WebUtility]::HtmlDecode($_.Groups[1].Value).Trim()
            }

            $sharedStrings += (($parts | ForEach-Object { $_ }) -join "")
        }

        [xml]$workbook = (New-Object IO.StreamReader($zip.GetEntry("xl/workbook.xml").Open())).ReadToEnd()
        [xml]$relationships = (New-Object IO.StreamReader($zip.GetEntry("xl/_rels/workbook.xml.rels").Open())).ReadToEnd()

        $workbookNs = New-Object System.Xml.XmlNamespaceManager($workbook.NameTable)
        $workbookNs.AddNamespace("d", "http://schemas.openxmlformats.org/spreadsheetml/2006/main")

        $relationshipNs = New-Object System.Xml.XmlNamespaceManager($relationships.NameTable)
        $relationshipNs.AddNamespace("r", "http://schemas.openxmlformats.org/package/2006/relationships")

        $sheetMap = @{}
        foreach ($sheet in $workbook.SelectNodes("//d:sheets/d:sheet", $workbookNs)) {
            $relationshipId = $sheet.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships")
            $relationship = $relationships.SelectSingleNode("//r:Relationship[@Id='$relationshipId']", $relationshipNs)
            $sheetMap[$sheet.GetAttribute("name")] = "xl/" + $relationship.GetAttribute("Target")
        }

        $rows = @()

        foreach ($sheetName in $sheetMap.Keys) {
            [xml]$sheet = (New-Object IO.StreamReader($zip.GetEntry($sheetMap[$sheetName]).Open())).ReadToEnd()
            $context = @{
                SiteCode  = ""
                Suburb    = ""
                Site      = ""
                Footcount = ""
                Lsm       = ""
            }

            foreach ($row in $sheet.worksheet.sheetData.row) {
                if ([int]$row.r -lt 2) {
                    continue
                }

                $cells = @{}
                foreach ($cell in $row.c) {
                    $column = ([string]$cell.r) -replace "\d", ""
                    $value = ""

                    if ($cell.t -eq "s") {
                        $value = $sharedStrings[[int]([string]$cell.v)]
                    }
                    elseif ($null -ne $cell.v) {
                        $value = [string]$cell.v
                    }
                    elseif ($cell.t -eq "inlineStr" -and $null -ne $cell.is.t) {
                        $value = [string]$cell.is.t
                    }

                    $cells[$column] = $value.Trim()
                }

                if ($cells["B"] -eq "Site #" -or $cells["F"] -eq "LSM") {
                    continue
                }

                if ([string]::IsNullOrWhiteSpace($cells["B"]) -and
                    [string]::IsNullOrWhiteSpace($cells["G"]) -and
                    [string]::IsNullOrWhiteSpace($cells["K"])) {
                    continue
                }

                foreach ($column in "B", "C", "D", "E", "F") {
                    if (-not $cells.ContainsKey($column) -or [string]::IsNullOrWhiteSpace($cells[$column])) {
                        continue
                    }

                    switch ($column) {
                        "B" { $context.SiteCode = $cells[$column] }
                        "C" { $context.Suburb = $cells[$column] }
                        "D" { $context.Site = $cells[$column] }
                        "E" { $context.Footcount = $cells[$column] }
                        "F" { $context.Lsm = $cells[$column] }
                    }
                }

                $prefix = if ($context.SiteCode -match "^[A-Z]+") { $Matches[0] } else { $sheetName }
                $province = switch -Regex ($prefix) {
                    "^GP" { "Gauteng" }
                    "^KZN" { "KwaZulu-Natal" }
                    "^WC" { "Western Cape" }
                    "^EC" { "Eastern Cape" }
                    "^NW" { "North West" }
                    default { $sheetName }
                }

                $rows += [pscustomobject]@{
                    Supplier    = $SupplierName
                    Province    = $province
                    SiteCode    = $context.SiteCode
                    Suburb      = $context.Suburb
                    Site        = $context.Site
                    Footcount   = $context.Footcount
                    Lsm         = $context.Lsm
                    Medium      = $cells["G"]
                    Format      = $cells["H"]
                    Layout      = $cells["I"]
                    Screens     = $cells["J"]
                    MonthlyRate = $cells["K"]
                }
            }
        }

        return $rows
    }
    finally {
        $zip.Dispose()
    }
}

$parsedRows = Parse-OohWorkbook -Path $WorkbookPath -SupplierName $Supplier
if ($parsedRows.Count -eq 0) {
    throw "No inventory rows were parsed from $WorkbookPath"
}

$tmpDirectory = Join-Path $PSScriptRoot "..\.tmp"
New-Item -ItemType Directory -Force -Path $tmpDirectory | Out-Null

$csvPath = Join-Path $tmpDirectory "ooh_workbook_stage.csv"
$parsedRows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$skipExistingSql = if ($SkipExistingSites) {
@"
create temporary table tmp_stage_skip as
select distinct p.*
from tmp_stage_prepared p
where exists (
  select 1
  from inventory_items_final e
  where lower(coalesce(e.supplier,'')) <> lower('$Supplier')
    and e.province = p.province
    and regexp_replace(lower(split_part(e.site_name,',,',1)), '[^a-z0-9]+', '', 'g') = p.site_key
);
"@
}
else {
@"
create temporary table tmp_stage_skip as
select *
from tmp_stage_prepared
where false;
"@
}

$sqlHeader = @"
begin;
drop table if exists tmp_stage_raw;
create temporary table tmp_stage_raw (
  supplier text,
  province text,
  site_code text,
  suburb text,
  site text,
  footcount text,
  lsm text,
  medium text,
  format text,
  layout text,
  screens text,
  monthly_rate text
);
copy tmp_stage_raw (supplier, province, site_code, suburb, site, footcount, lsm, medium, format, layout, screens, monthly_rate) from stdin with (format csv, header true);
"@

$sqlTail = @"
\.

create temporary table tmp_stage_prepared as
select
  supplier,
  province,
  site_code,
  suburb,
  site,
  nullif(regexp_replace(footcount, '[^0-9]', '', 'g'), '')::bigint as footcount,
  lsm,
  initcap(lower(medium)) as medium,
  case
    when lower(format) like '%billboard%' then 'Billboard'
    when lower(format) like '%screen%' then 'Screen'
    else initcap(lower(format))
  end as format,
  case
    when lower(layout) = 'landscape/portrait' then 'Landscape/Portrait'
    else initcap(lower(layout))
  end as layout,
  nullif(regexp_replace(screens, '[^0-9]', '', 'g'), '')::int as screens,
  nullif(regexp_replace(monthly_rate, '[^0-9.]', '', 'g'), '')::numeric(18,2) as monthly_rate_zar,
  regexp_replace(lower(site), '[^a-z0-9]+', '', 'g') as site_key
from tmp_stage_raw;

$skipExistingSql

create temporary table tmp_stage_importable as
select p.*
from tmp_stage_prepared p
where not exists (
  select 1
  from tmp_stage_skip s
  where s.site_code = p.site_code
    and s.site = p.site
    and s.province = p.province
    and coalesce(s.medium,'') = coalesce(p.medium,'')
    and coalesce(s.format,'') = coalesce(p.format,'')
    and coalesce(s.layout,'') = coalesce(p.layout,'')
    and coalesce(s.screens,-1) = coalesce(p.screens,-1)
    and coalesce(s.monthly_rate_zar,-1) = coalesce(p.monthly_rate_zar,-1)
);

drop table if exists tmp_stage_updated;
create temporary table tmp_stage_updated as
with updated as (
  update inventory_items_final tgt
  set
    media_type = concat_ws(' | ', src.medium, src.format, nullif(src.layout, '')),
    site_name = src.site,
    city = src.suburb,
    suburb = src.suburb,
    province = src.province,
    address = concat_ws(', ', src.site, src.suburb, src.province),
    metadata_json = jsonb_strip_nulls(jsonb_build_object(
      'supplier', '$Supplier',
      'site_code', src.site_code,
      'site_name', src.site,
      'suburb', src.suburb,
      'province', src.province,
      'footcount', src.footcount,
      'lsm', src.lsm,
      'medium', src.medium,
      'format', src.format,
      'layout', src.layout,
      'screens', src.screens,
      'monthly_rate_zar', src.monthly_rate_zar,
      'available', true,
      'source_file', '$SourceFileName',
      'inventory_source', '$InventorySource',
      'pricing_model', 'per_site_monthly'
    ))
  from tmp_stage_importable src
  where lower(coalesce(tgt.supplier,'')) = lower('$Supplier')
    and coalesce(tgt.metadata_json ->> 'site_code', '') = src.site_code
    and lower(coalesce(tgt.metadata_json ->> 'medium', '')) = lower(src.medium)
    and lower(coalesce(tgt.metadata_json ->> 'format', '')) = lower(src.format)
    and lower(coalesce(tgt.metadata_json ->> 'layout', '')) = lower(src.layout)
  returning tgt.id
)
select * from updated;

drop table if exists tmp_stage_inserted;
create temporary table tmp_stage_inserted as
with inserted as (
  insert into inventory_items_final (
    supplier,
    media_type,
    site_name,
    city,
    suburb,
    province,
    address,
    metadata_json
  )
  select
    '$Supplier',
    concat_ws(' | ', src.medium, src.format, nullif(src.layout, '')),
    src.site,
    src.suburb,
    src.suburb,
    src.province,
    concat_ws(', ', src.site, src.suburb, src.province),
    jsonb_strip_nulls(jsonb_build_object(
      'supplier', '$Supplier',
      'site_code', src.site_code,
      'site_name', src.site,
      'suburb', src.suburb,
      'province', src.province,
      'footcount', src.footcount,
      'lsm', src.lsm,
      'medium', src.medium,
      'format', src.format,
      'layout', src.layout,
      'screens', src.screens,
      'monthly_rate_zar', src.monthly_rate_zar,
      'available', true,
      'source_file', '$SourceFileName',
      'inventory_source', '$InventorySource',
      'pricing_model', 'per_site_monthly'
    ))
  from tmp_stage_importable src
  where not exists (
    select 1
    from inventory_items_final tgt
    where lower(coalesce(tgt.supplier,'')) = lower('$Supplier')
      and coalesce(tgt.metadata_json ->> 'site_code', '') = src.site_code
      and lower(coalesce(tgt.metadata_json ->> 'medium', '')) = lower(src.medium)
      and lower(coalesce(tgt.metadata_json ->> 'format', '')) = lower(src.format)
      and lower(coalesce(tgt.metadata_json ->> 'layout', '')) = lower(src.layout)
  )
  returning id
)
select * from inserted;

select 'prepared_rows' as metric, count(*)::text as value from tmp_stage_prepared
union all
select 'skip_rows', count(*)::text from tmp_stage_skip
union all
select 'updated_rows', count(*)::text from tmp_stage_updated
union all
select 'inserted_rows', count(*)::text from tmp_stage_inserted
union all
select 'supplier_total_rows', count(*)::text from inventory_items_final where lower(coalesce(supplier,'')) = lower('$Supplier')
union all
select 'inventory_total_rows', count(*)::text from inventory_items_final;

commit;
"@

$sqlPath = Join-Path $tmpDirectory "ooh_workbook_import.sql"
Set-Content -LiteralPath $sqlPath -Value (($sqlHeader -split "`r?`n") + (Get-Content -LiteralPath $csvPath) + ($sqlTail -split "`r?`n"))

Get-Content -LiteralPath $sqlPath | ssh -i $SshKeyPath "$SshUser@$SshHost" "docker exec -i $DbContainer psql -U $DbUser -d $Database"

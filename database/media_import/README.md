# Media Import Pipeline

This folder packages the radio and OOH seed import into a repeatable 4-step workflow:

1. Create staging and final tables
2. Load CSVs into staging with `\copy`
3. Generate normalized radio/SABC seed CSVs from `raw_pdf_pages.csv`
4. Transform staging data into final tables

## Files

- `001_create_tables.sql`
- `002_copy_seed_data.sql`
- `003_transform_final.sql`
- `../../tools/media_import/normalize_radio_from_raw_pages.py`
- `../../tools/media_import/stage_seed_files.ps1`
- `../../tools/media_import/run_media_import.ps1`

## Expected CSVs

Point `seed_dir` at the folder containing:

- `import_manifest.csv`
- `raw_pdf_pages.csv`
- `package_document_metadata.csv`
- `ooh_inventory_blackspace.csv`
- `package_summary_seed.csv`
- `radio_slot_grid_seed.csv`
- `sabc_rate_table_seed.csv`

Recommended repo-owned location:

- `database/media_import/seed`

## Optional. Stage files into the repo seed folder

```powershell
powershell -ExecutionPolicy Bypass -File tools/media_import/stage_seed_files.ps1
```

That copies the required CSVs from `Downloads` into [seed](c:/Users/CC%20KEMPTON/source/Advertified_V4/database/media_import/seed).

## Step 1. Create tables

```powershell
psql "Host=localhost;Port=5432;Database=Advertified;Username=postgres;Password=YOUR_PASSWORD_HERE" `
  -f database/media_import/001_create_tables.sql
```

## Step 2. Run the normalizer

This regenerates the radio seed outputs from `raw_pdf_pages.csv`.

```powershell
python tools/media_import/normalize_radio_from_raw_pages.py `
  "C:\Users\CC KEMPTON\source\Advertified_V4\database\media_import\seed\raw_pdf_pages.csv" `
  "C:\Users\CC KEMPTON\source\Advertified_V4\database\media_import\seed"
```

## Step 3. Load CSVs into staging

Use `\copy`, not server-side `COPY`, so local Windows paths work.

```powershell
psql "Host=localhost;Port=5432;Database=Advertified;Username=postgres;Password=YOUR_PASSWORD_HERE" `
  -v seed_dir="'C:/Users/CC KEMPTON/source/Advertified_V4/database/media_import/seed'" `
  -f database/media_import/002_copy_seed_data.sql
```

## Step 4. Transform staging into final tables

```powershell
psql "Host=localhost;Port=5432;Database=Advertified;Username=postgres;Password=YOUR_PASSWORD_HERE" `
  -f database/media_import/003_transform_final.sql
```

## Notes

- The simplified `COPY inventory_items / radio_packages / radio_slots` shape from early draft notes does not match the actual CSV headers in Downloads. These scripts match the real file layouts.
- `radio_slot_grid_seed.csv` does not contain a direct monetary rate column, so rows loaded from that file land in `radio_slots_final` with `rate = NULL` and their window details preserved in `metadata_json`.
- SABC rows use `avg_cost_per_spot_zar` when present, otherwise derive a per-spot rate from `package_cost_zar / spots_count`.

## One-command runner

Once `psql` and Python are installed and available in `PATH`, you can run the entire workflow with:

```powershell
powershell -ExecutionPolicy Bypass -File tools/media_import/run_media_import.ps1
```

Optional examples:

```powershell
powershell -ExecutionPolicy Bypass -File tools/media_import/run_media_import.ps1 `
  -SeedDir "C:\Users\CC KEMPTON\source\Advertified_V4\database\media_import\seed"
```

```powershell
powershell -ExecutionPolicy Bypass -File tools/media_import/run_media_import.ps1 `
  -SkipNormalize
```

If `python` or `psql` are installed but not available in `PATH`, pass them explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File tools/media_import/run_media_import.ps1 `
  -PythonPath "C:\Path\To\python.exe" `
  -PsqlPath "C:\Path\To\psql.exe"
```

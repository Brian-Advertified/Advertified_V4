# Google Sheets Templates

These CSV files are ready to upload into Google Sheets.

## Files

- `lead-intake-template.csv`
  - Use this as the clean intake sheet for leads coming into Advertified.
- `lead-control-tower-template.csv`
  - Use this as the destination structure for the Lead Ops webhook export.

## Recommended setup

1. Create a new Google Sheet workbook in Workspace.
2. Import `lead-intake-template.csv` into a tab named `Lead Intake`.
3. Import `lead-control-tower-template.csv` into a tab named `Lead Control Tower`.
4. Add the Apps Script webhook from:
   - `tools/google-apps-script/lead-control-tower`
5. Point `GoogleSheetsLeadOps:ExportWebhookUrl` at the deployed Apps Script URL.
6. Use the `Lead Intake` tab as your human-editable source list.
7. Let Advertified update the `Lead Control Tower` tab from the webhook.

## Notes

- `source_reference` should stay stable for each row. That is the best way to avoid duplicates.
- The current lead importer uses:
  - `business_name` via the alias `name` is not automatic yet, so when publishing to CSV we should either:
    - rename `business_name` to `name`, or
    - add a simple import mapping update in code.
- If we want zero-friction import from this exact template, the next code tweak should add `business_name` as an accepted alias for `name`.

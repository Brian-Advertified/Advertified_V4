# Google Sheets Templates

These CSV files are ready to upload into Google Sheets.

## Files

- `lead-intake-template.csv`
  - Use this as the clean intake sheet for leads coming into Advertified.
- `lead-control-tower-template.csv`
  - Use this as the human-facing destination structure for the Lead Ops webhook export.

## Recommended setup

1. Create a new Google Sheet workbook in Workspace.
2. Import `lead-intake-template.csv` into a tab named `Lead Intake`.
3. Import `lead-control-tower-template.csv` into a tab named `Lead Control Tower`.
4. Add the Apps Script webhook from:
   - `tools/google-apps-script/lead-control-tower`
5. Point `GoogleSheetsLeadOps:ExportWebhookUrl` at the deployed Apps Script URL.
6. Use the `Lead Intake` tab as your human-editable source list.
7. Let Advertified update the `Lead Control Tower` tab from the webhook.

The Apps Script will also create and maintain:
- `Lead Control Sync`
  - Hidden system tab used for raw row upserts and auditability.
- `Lead Control Totals`
  - Operating totals and conversion summary tab.

## Notes

- `source_reference` should stay stable for each row. That is the best way to avoid duplicates.
- The current lead importer already accepts `business_name` because headers are normalized before matching.
- The control tower template is intentionally simple for humans. Internal IDs and raw workflow fields now live in the hidden sync tab instead of the visible operations tab.
- The visible control tower now mirrors the app's canonical control-tower rows directly instead of rebuilding queue logic inside Apps Script.
- The `notes` column is preserved across syncs so Workspace users can keep lightweight collaboration notes there.

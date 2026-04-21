# Lead Control Tower Apps Script

This Google Apps Script receives the Advertified Lead Ops snapshot webhook and writes it into Google Sheets with row upserts keyed by `item.id`.

## What it writes

It maintains two tabs:

- `Lead Control Tower`
- `Lead Control Totals`

`Lead Control Tower` stores one row per Lead Ops item and updates the same row on later snapshots.
Rows missing from a later full snapshot are marked `missing_from_snapshot` instead of being deleted.

## Important auth note

Google Apps Script web apps do not reliably expose custom request headers to `doPost(e)`.
Because of that, the app's `X-Advertified-Token` header is not a dependable auth check for this receiver.

Use a token in the webhook URL instead:

```text
https://script.google.com/macros/s/DEPLOYMENT_ID/exec?token=YOUR_SHARED_TOKEN
```

Then set the same token in the script property `LEAD_CONTROL_WEBHOOK_TOKEN`.

## Script properties

Open the Apps Script project and set these in `Project Settings -> Script properties`:

- `LEAD_CONTROL_WEBHOOK_TOKEN`
  - Required if you want webhook protection.
- `LEAD_CONTROL_TOWER_SHEET_NAME`
  - Optional. Defaults to `Lead Control Tower`.
- `LEAD_CONTROL_TOTALS_SHEET_NAME`
  - Optional. Defaults to `Lead Control Totals`.
- `LEAD_CONTROL_APP_BASE_URL`
  - Optional. Example: `https://dev.advertified.com`
  - Used to build absolute `route_url` values from `route_path`.
- `LEAD_CONTROL_ARCHIVE_MISSING_ITEMS`
  - Optional. Defaults to `true`.
  - When `true`, rows missing from the latest snapshot are marked `missing_from_snapshot`.

## Deploy

1. Create a bound or standalone Apps Script project.
2. Copy in `Code.gs` and `appsscript.json`.
3. Set the script properties above.
4. Run `setupLeadControlTower` once from the editor.
5. Deploy as a web app:
   - Execute as: `Me`
   - Who has access: `Anyone`
6. Copy the deployed `/exec` URL.

## Advertified app config

Point `GoogleSheetsLeadOps:ExportWebhookUrl` to the web app URL with the token query param:

```json
{
  "GoogleSheetsLeadOps": {
    "Enabled": true,
    "ExportEnabled": true,
    "ExportWebhookUrl": "https://script.google.com/macros/s/DEPLOYMENT_ID/exec?token=YOUR_SHARED_TOKEN",
    "ExportWebhookAuthToken": ""
  }
}
```

Leave `ExportWebhookAuthToken` blank for this receiver, since the Apps Script validates the query token instead.

## Expected webhook payload

The current app sends:

```json
{
  "generatedAtUtc": "2026-04-21T18:00:00Z",
  "totals": {
    "totalItems": 12,
    "urgentCount": 4
  },
  "items": [
    {
      "id": "open_lead_action:42",
      "itemType": "open_lead_action",
      "title": "Call Fit Lab",
      "routePath": "/agent/lead-intelligence?leadId=42"
    }
  ]
}
```

## Sheet columns

The tower tab stores:

- `record_key`
- `item_type`
- `item_label`
- `campaign_id`
- `prospect_lead_id`
- `lead_id`
- `lead_action_id`
- `title`
- `subtitle`
- `description`
- `unified_status`
- `assigned_agent_user_id`
- `assigned_agent_name`
- `is_assigned_to_current_user`
- `is_unassigned`
- `is_urgent`
- `route_path`
- `route_url`
- `route_label`
- `created_at`
- `updated_at`
- `due_at`
- `snapshot_generated_at`
- `snapshot_status`
- `last_seen_at`

## Operational notes

- This receiver treats each webhook call as a full snapshot.
- It upserts active rows by `record_key`.
- It does not delete rows.
- It marks unseen rows as `missing_from_snapshot` so the sheet remains auditable.

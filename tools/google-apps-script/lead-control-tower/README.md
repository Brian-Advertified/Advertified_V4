# Lead Control Tower Apps Script

This Google Apps Script receives the Advertified Lead Ops control-tower webhook and writes it into Google Sheets with row upserts keyed by `record_key`.
It keeps the raw sync data hidden, then mirrors a cleaner CRM-style operations tab for humans.

## What it writes

It maintains three tabs:

- `Lead Control Tower`
- `Lead Control Sync`
- `Lead Control Totals`

- `Lead Control Tower` is the human-facing tab. It shows one row per lead or deal, with plain-language columns like source, owner, assignment status, lifecycle stage, contact status, next action, and a direct open link.
- `Lead Control Sync` is the hidden system tab. It stores the full raw webhook snapshot and is used for reliable row upserts and auditability.
- `Lead Control Totals` stores the current operating totals and conversion summary.

Rows missing from a later full snapshot are marked as no longer active instead of being deleted.

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
- `LEAD_CONTROL_SYNC_SHEET_NAME`
  - Optional. Defaults to `Lead Control Sync`.
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
    "totalLeadCount": 42,
    "leadToSaleRatePercent": 14.3
  },
  "sources": [
    {
      "source": "google_sheet",
      "leadCount": 18,
      "prospectCount": 7,
      "wonCount": 2
    }
  ],
  "items": [
    {
      "recordKey": "campaign:13ed461e-e00b-4da4-8ff0-c4488d0364b1",
      "leadId": 42,
      "leadName": "Brian Rabuthu Launch Campaign",
      "source": "public_questionnaire",
      "unifiedStatus": "proposal_in_progress",
      "assignmentStatus": "unassigned",
      "contactStatus": "not_contacted",
      "priority": "high",
      "attentionReasons": ["unassigned", "needs_follow_up"],
      "routePath": "/agent/campaigns/13ed461e-e00b-4da4-8ff0-c4488d0364b1"
    }
  ]
}
```

## Human-facing sheet columns

The `Lead Control Tower` tab stores:

- `lead_name`
- `location`
- `category`
- `source`
- `owner`
- `assignment_status`
- `lifecycle_stage`
- `contact_status`
- `next_action`
- `next_action_due_at`
- `next_follow_up_at`
- `sla_due_at`
- `priority`
- `attention_reasons`
- `last_outcome`
- `notes`
- `open_in_advertified`
- `last_updated_at`

The internal `record_key` column is still present but hidden so the script can upsert the right row safely.

`notes` is intentionally preserved on row updates so Workspace users can keep lightweight operational notes there without turning Sheets into the source of truth.

## Raw sync sheet columns

The hidden `Lead Control Sync` tab stores:

- `record_key`
- `lead_id`
- `lead_name`
- `location`
- `category`
- `source`
- `source_reference`
- `unified_status`
- `owner_agent_user_id`
- `owner_agent_name`
- `owner_resolution`
- `assignment_status`
- `has_been_contacted`
- `first_contacted_at`
- `contact_status`
- `last_contacted_at`
- `next_action`
- `next_action_due_at`
- `next_follow_up_at`
- `sla_due_at`
- `priority`
- `attention_reasons`
- `open_lead_action_count`
- `has_prospect`
- `prospect_lead_id`
- `active_campaign_id`
- `won_campaign_id`
- `converted_to_sale`
- `last_outcome`
- `route_path`
- `route_url`
- `snapshot_generated_at`
- `snapshot_status`
- `last_seen_at`

## Operational notes

- This receiver treats each webhook call as a full snapshot.
- It upserts rows by `record_key`.
- It does not delete rows.
- It hides the raw sync tab so people work from the cleaner operations view instead of the raw payload mirror.
- The app is now responsible for deduping and lifecycle logic; the script only mirrors the canonical control-tower rows.
- If an item disappears from a later snapshot, the visible row is marked in `attention_reasons` as `No longer active` instead of being deleted.
- The visible `notes` column is preserved across syncs so Workspace users can collaborate safely without editing the operational source of truth.

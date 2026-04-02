# AI Dead-Letter Replay Runbook

## Purpose
Recover failed AI jobs (creative and asset pipelines) in a controlled way.

## Preconditions
- Operator is an `admin` user.
- Root cause has been checked (provider outage, credential issue, malformed payload, queue outage).
- Retry window and cost impact are acceptable.

## Endpoints
- Replay creative failed job:
  - `POST /admin/ai/jobs/creative/{jobId}/replay`
- Replay asset failed job:
  - `POST /admin/ai/jobs/assets/{jobId}/replay`

## Step-by-Step
1. Confirm job is failed.
   - Creative: check `ai_creative_job_statuses.status = 'failed'`.
   - Asset: check `ai_asset_jobs.status = 'failed'`.
2. Confirm failure reason in `last_failure`/`error` and verify it is resolved.
3. Replay using admin endpoint.
4. Monitor:
   - `admin/dashboard` monitoring panel (alerts/backlog/dead-letter count).
   - Job-specific status endpoint for creative/asset.
5. If replay fails repeatedly:
   - Stop replay loop.
   - Escalate incident.
   - Capture provider response and queue delivery count.

## SQL Checks
```sql
-- Creative failed jobs
select job_id, campaign_id, status, retry_attempt_count, last_failure, updated_at
from ai_creative_job_statuses
where status = 'failed'
order by updated_at desc
limit 50;

-- Asset failed jobs
select id as job_id, campaign_id, asset_kind, provider, status, retry_attempt_count, last_failure, updated_at
from ai_asset_jobs
where status = 'failed'
order by updated_at desc
limit 50;
```

## Safety Notes
- Replay can consume AI budget again.
- Cost guard can auto-stop ad operations if campaign AI cap is breached.
- Do not replay without validating provider credentials/endpoints first.

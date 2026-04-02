# Ad Ops Production Rollout Checklist

## 1) Database + migration rollout
1. Backup production database.
2. Apply:
   - `database/bootstrap/032_ai_ad_operations.sql`
3. Verify tables/indexes:
```sql
select to_regclass('public.ai_ad_variants') as ai_ad_variants;
select to_regclass('public.ai_ad_metrics') as ai_ad_metrics;
```
4. Smoke queries:
```sql
select count(*) from ai_ad_variants;
select count(*) from ai_ad_metrics;
```

## 2) Queue and infra
1. Set `AiPlatform:UseInMemoryFallback=false`.
2. Set `AiPlatform:ServiceBusConnectionString` and queue names.
3. Restart API/workers.
4. Validate workers are processing (no growing backlog for queued jobs).

## 3) Ad provider live config
1. Set `AdPlatforms:DryRunMode=false`.
2. Fill live credentials:
   - `AdPlatforms:Meta:*`
   - `AdPlatforms:GoogleAds:*`
3. Validate against sandbox campaigns first:
   - Create variant
   - Publish variant
   - Sync metrics
   - Optimize

## 4) Tracking implementation
1. Enable Meta Pixel/CAPI and Google conversion tracking.
2. Ensure conversion events include `ad_variant_id`.
3. Validate event-to-variant attribution in DB.

## 5) Authorization hardening
1. Verify role/ownership path tests pass.
2. Confirm client restrictions (publish/sync/optimize blocked).
3. Confirm agent assignment restrictions.

## 6) Observability + alerts
Monitor:
- Publish success/failure
- Metrics sync lag
- Queue backlog/retries/dead-letter
- Cost cap rejections and utilization

Alert examples:
- `failed` + retries `>= 3`
- queue backlog growth over threshold
- cost rejection spike by campaign/day

## 7) Cost control enforcement
1. Verify campaign cost guard blocks over-cap operations.
2. Verify auto-stop marks active ad variants as `cost_stopped`.
3. Review monthly provider report:
   - `GET /admin/ai/cost-reports/monthly?months=6`

## 8) Release process
1. Staging sign-off (end-to-end path complete).
2. Deploy via blue/green (preferred) or canary.
3. Run smoke tests on new environment.
4. Rollback plan ready:
   - previous app version
   - DB rollback script (or restore from backup if required)

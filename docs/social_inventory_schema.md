# Social Inventory Plan

## Recommended Phase 1 Platforms

Implement these first:

- `meta`
- `tiktok`
- `youtube`
- `linkedin`

Reason:

- They cover the strongest commercial mix for Advertified today.
- They map well to the engine's current strategic inputs: objective, audience type, buying behaviour, price positioning, geography, and budget.
- They can be represented as benchmark inventory without pretending that social has fixed supplier rate cards.

Defer to Phase 2:

- `pinterest`
- `snapchat`

Do not implement first:

- `x`
- `threads`
- `whatsapp_ads`

## Key Principle

Do not create a parallel inventory system.

Use the existing normalized inventory model:

- `media_outlet`
- `media_outlet_language`
- `media_outlet_keyword`
- `media_outlet_geography`
- `media_outlet_pricing_package`
- `strategy_fit_json` on `media_outlet`

For social, each platform becomes a `media_outlet`.

Examples:

- `meta` = one outlet covering Facebook + Instagram inventory
- `tiktok` = one outlet
- `youtube` = one outlet
- `linkedin` = one outlet

## Existing Schema Mapping

### `media_outlet`

Use:

- `code`
- `name`
- `media_type`
- `coverage_type`
- `catalog_health`
- `operator_name`
- `is_national`
- `has_pricing`
- `target_audience`
- `data_source_enrichment`
- `strategy_fit_json`

Recommended social values:

- `media_type`: `digital`
- `coverage_type`: `national`
- `catalog_health`: `healthy`
- `is_national`: `true`
- `has_pricing`: `true`

### `media_outlet_pricing_package`

Use packages as benchmark buying profiles, not fixed supplier slots.

Recommended package rows:

- one row per platform + objective cluster
- package names like:
  - `Awareness benchmark`
  - `Traffic benchmark`
  - `Lead generation benchmark`
  - `Video views benchmark`

Use current columns as follows:

- `package_name`: benchmark package label
- `package_type`: `social_benchmark`
- `investment_zar`: estimated working media spend
- `cost_per_month_zar`: estimated monthly benchmark spend
- `value_zar`: optional planning reference value
- `notes`: JSON-like text or structured note describing billing model and benchmark ranges
- `source_name`: benchmark source label
- `source_date`: source snapshot date

## Recommended Social Fields

These can live in `data_source_enrichment` or `notes` immediately.

If we later want stronger admin support, these are the first columns I would add to `media_outlet_pricing_package`:

- `billing_model`
- `billing_event`
- `benchmark_cpm_min_zar`
- `benchmark_cpm_max_zar`
- `benchmark_cpc_min_zar`
- `benchmark_cpc_max_zar`
- `benchmark_cpv_min_zar`
- `benchmark_cpv_max_zar`
- `minimum_daily_budget_zar`
- `minimum_campaign_budget_zar`
- `recommended_daily_budget_zar`
- `recommended_learning_period_days`
- `creative_format_family`

If we do not want new columns yet, store these in `notes` JSON and normalize later.

## Pricing Columns To Use

For benchmark social inventory:

- `investment_zar`
  - Use as the recommended entry spend for planning.
- `cost_per_month_zar`
  - Use as the benchmark monthly spend.
- `value_zar`
  - Optional reference value only. Do not treat this as guaranteed inventory value.

Do not use `slot_rate` for social inventory.

## Objective Mapping

### Meta

- Primary:
  - `awareness`
  - `lead_generation`
- Secondary:
  - `website_traffic`
  - `retargeting`

### TikTok

- Primary:
  - `awareness`
  - `video_views`
- Secondary:
  - `website_traffic`
  - `lead_generation`

### YouTube

- Primary:
  - `awareness`
  - `video_views`
- Secondary:
  - `website_traffic`

### LinkedIn

- Primary:
  - `lead_generation`
  - `awareness`
- Secondary:
  - `website_traffic`
  - `b2b_consideration`

## Strategy Fit Mapping

Recommended `strategy_fit_json` defaults:

### Meta

- `sales_model_fit`: `online_sales,appointment_booking,in_store`
- `buying_behaviour_fit`: `impulse,mixed,price_sensitive`
- `price_positioning_fit`: `budget,mid_market,premium`
- `objective_fit_primary`: `awareness`
- `objective_fit_secondary`: `lead_generation`
- `environment_type`: `social_feed`
- `premium_mass_fit`: `mass_to_mid`
- `data_confidence`: `medium`

### TikTok

- `sales_model_fit`: `online_sales`
- `buying_behaviour_fit`: `impulse,discovery_led`
- `price_positioning_fit`: `budget,mid_market`
- `objective_fit_primary`: `awareness`
- `objective_fit_secondary`: `video_views`
- `environment_type`: `short_form_video`
- `premium_mass_fit`: `mass`
- `data_confidence`: `medium`

### YouTube

- `sales_model_fit`: `online_sales,brand_building`
- `buying_behaviour_fit`: `considered,mixed`
- `price_positioning_fit`: `mid_market,premium`
- `objective_fit_primary`: `awareness`
- `objective_fit_secondary`: `video_views`
- `environment_type`: `long_form_video`
- `premium_mass_fit`: `mass_to_premium`
- `data_confidence`: `medium`

### LinkedIn

- `sales_model_fit`: `sales_team,appointment_booking,b2b`
- `buying_behaviour_fit`: `considered,high_intent`
- `price_positioning_fit`: `mid_market,premium`
- `objective_fit_primary`: `lead_generation`
- `objective_fit_secondary`: `awareness`
- `environment_type`: `professional_feed`
- `premium_mass_fit`: `premium`
- `data_confidence`: `medium`

## Benchmark Ranges

These are not fixed prices. They are planning benchmarks.

Working FX assumption for the seed file:

- `1 USD = 16.8331 ZAR`
- source snapshot date: `2026-04-07`
- source: Wise USD/ZAR mid-market page

### Official platform minimums and billing behavior

- Meta:
  - auction-based
  - budget and cost vary by audience, objective, bid, and schedule
  - Meta recommends setting enough budget over at least 7 days so the system can learn
- TikTok:
  - minimum budget: `$50` campaign level
  - minimum budget: `$20` ad group level
- LinkedIn:
  - auction-based
  - campaign total budget requires at least `$100` unspent budget to launch new ad sets
- YouTube / Google Ads:
  - auction-based by default
  - video campaigns use daily or total campaign budget
  - CPV and conversion bidding models are supported

### Benchmark ranges used for seed planning

- Meta:
  - CPM: `$1.01 - $4.00`
  - CPC: `$0.26 - $0.50`
- TikTok:
  - CPM: `$3.21 - $10.00`
  - CPC: `$0.25 - $4.00`
  - CPV: `$0.01 - $0.30`
- YouTube:
  - CPM: `$9.68`
  - CPC: `$0.11 - $0.40`
  - CPV: `$0.31 - $0.40`
- LinkedIn:
  - CPM: `$5.01 - $8.00`
  - CPC: `$2.00 - $3.00`

## Recommended Next Implementation Step

Implement social as benchmark inventory only:

1. seed the 4 phase-1 outlets
2. seed objective benchmark packages
3. load strategy-fit defaults
4. expose them as `digital` in planning
5. keep them clearly labeled as `benchmark / auction-based`

Do not represent them as guaranteed fixed supplier placements.

## Sources

- Meta pricing overview:
  - https://www.facebook.com/business/ads/pricing
- Meta help snippets on ads pricing and budget behavior:
  - https://www.facebook.com/business/ads/pricing
  - https://www.facebook.com/help/447278887528796
- TikTok budget and bidding:
  - https://ads.tiktok.com/help/article/budget-and-bidding-faq
- LinkedIn pricing:
  - https://business.linkedin.com/advertise/ads/pricing
- LinkedIn minimum campaign budget requirement:
  - https://www.linkedin.com/help/lms/answer/a9486341
- LinkedIn campaign budgets:
  - https://www.linkedin.com/help/linkedin/answer/a422101
- Snapchat pricing:
  - https://forbusiness.snapchat.com/advertising/pricing
- Pinterest bid floor reference:
  - https://help.pinterest.com/en/business/article/set-your-bid
- Google Ads budgeting:
  - https://support.google.com/google-ads/answer/2375454
- Google Ads CPV:
  - https://support.google.com/google-ads/answer/2382888
- Google reservation video buying:
  - https://support.google.com/google-ads/answer/9547606
- Wise USD/ZAR rate snapshot:
  - https://wise.com/us/currency-converter/usd-to-zar-rate/history
- WebFX benchmark references:
  - https://www.webfx.com/social-media/pricing/how-much-does-social-media-advertising-cost/
  - https://www.webfx.com/social-media/pricing/how-much-does-linkedin-advertising-cost/
  - https://www.webfx.com/blog/social-media/tiktok-benchmarks/
  - https://www.webfx.com/social-media/pricing/how-much-does-youtube-advertising-cost/

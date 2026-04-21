# OOH Inventory Import Schema

This is the recommended structure for the new OOH CSV or JSON handoff.

Use one row per OOH inventory item that you want the system to store.

## Required Fields

| Field | Type | Example | What it means |
| --- | --- | --- | --- |
| `supplier` | text | `Eleven8` | Owner or media supplier |
| `site_name` | text | `Waterstone Village` | Human-readable site/location name |

## Strongly Recommended Fields

| Field | Type | Example | What it means |
| --- | --- | --- | --- |
| `site_code` | text | `WC19` | Supplier site code or internal site reference |
| `city` | text | `Somerset West` | City or nearest city |
| `suburb` | text | `Somerset West` | Suburb / local area / mall node |
| `province` | text | `Western Cape` | Province name |
| `media_type` | text | `Digital Screen \| Indoor` | Placement/media type shown to planner |
| `address` | text | `Waterstone Village, Somerset West, Western Cape` | Plain-language address |
| `latitude` | decimal | `-34.0821` | Decimal latitude |
| `longitude` | decimal | `18.8453` | Decimal longitude |
| `is_available` | boolean | `true` | `true` or `false` |
| `discounted_rate_zar` | number | `18000` | Sellable / discounted monthly or placement price |
| `rate_card_zar` | number | `22000` | Rate card / list price |
| `monthly_rate_zar` | number | `18000` | Monthly price when applicable |
| `traffic_count` | integer | `950000` | Monthly footfall / traffic estimate if known |

## Intelligence Fields

| Field | Type | Example | What it means |
| --- | --- | --- | --- |
| `venue_type` | text | `lifestyle_centre` | Site environment category |
| `premium_mass_fit` | text | `premium` | Premium vs mass-market fit |
| `price_positioning_fit` | text | `premium` | Price-positioning fit for brands |
| `audience_income_fit` | text | `lsm_8_10` | Income / LSM fit |
| `youth_fit` | text | `medium` | Suitability for youth targeting |
| `family_fit` | text | `high` | Suitability for family targeting |
| `professional_fit` | text | `high` | Suitability for professionals |
| `commuter_fit` | text | `low` | Suitability for commuter-driven campaigns |
| `tourist_fit` | text | `high` | Suitability for tourist exposure |
| `high_value_shopper_fit` | text | `high` | Fit for affluent / high-value shoppers |
| `audience_age_skew` | text | `25-34` | Dominant age signal |
| `audience_gender_skew` | text | `balanced` | Gender skew |
| `dwell_time_score` | text | `high` | Expected dwell time |
| `environment_type` | text | `mall_interior` | Broader environment classification |
| `buying_behaviour_fit` | text | `aspirational` | Purchase mode / shopper behavior |
| `primary_audience_tags` | semicolon list | `professionals; youth; tourists` | Main audience tags |
| `secondary_audience_tags` | semicolon list | `leisure; coastal` | Secondary audience tags |
| `recommendation_tags` | semicolon list | `premium_branding; high_consideration` | Planner/explainability tags |
| `intelligence_notes` | text | `Waterstone Village is treated as...` | Plain-English planner note |
| `data_confidence` | text | `high` | Confidence in the intelligence |
| `updated_by` | text | `OpenAI_web_rules_2026-04-16` | Who produced the row |

## Optional Metadata Fields

Any extra columns in the CSV will be preserved into `metadata_json`.

Recommended optional metadata fields:

| Field | Type | Example | What it means |
| --- | --- | --- | --- |
| `inventory_rows` | integer | `1` | Number of represented placements if this row is aggregated |
| `source_urls` | text | `https://site-a ; https://site-b` | Source links used for enrichment |
| `import_source` | text | `ooh_inventory_intelligence_enriched_2026-04-16.csv` | File/source provenance |
| `notes` | text | `Mall website confirms premium positioning` | Free-form source note |

## Value Guidance

- Use blank values when genuinely unknown.
- Use decimal coordinates, not DMS.
- Use semicolons for tag lists.
- Use numbers only for pricing and traffic fields. No `R`, commas, or extra text.
- Keep `site_name` stable across refreshes.
- Prefer full province names like `Western Cape`, `Gauteng`, `Eastern Cape`.

## Minimal CSV Header

```csv
supplier,site_code,site_name,city,suburb,province,media_type,address,latitude,longitude,is_available,discounted_rate_zar,rate_card_zar,monthly_rate_zar,traffic_count,venue_type,premium_mass_fit,price_positioning_fit,audience_income_fit,youth_fit,family_fit,professional_fit,commuter_fit,tourist_fit,high_value_shopper_fit,audience_age_skew,audience_gender_skew,dwell_time_score,environment_type,buying_behaviour_fit,primary_audience_tags,secondary_audience_tags,recommendation_tags,intelligence_notes,data_confidence,updated_by,inventory_rows,source_urls,import_source
```

## JSON Shape

Use an array of objects:

```json
[
  {
    "supplier": "Eleven8",
    "site_code": "WC19",
    "site_name": "Waterstone Village",
    "city": "Somerset West",
    "suburb": "Somerset West",
    "province": "Western Cape"
  }
]
```

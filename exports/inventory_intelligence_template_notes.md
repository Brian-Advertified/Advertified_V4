`inventory_intelligence_template.csv` guide

Main file:
- [inventory_intelligence_template.csv](c:/Users/CC%20KEMPTON/source/Advertified_V4/exports/inventory_intelligence_template.csv)

What is already filled:
- `existing_*` columns reflect metadata already present in the dev inventory.
- Core row identity and pricing columns are already populated.

What you should mainly populate:
- `enrich_target_audience`
- `enrich_audience_age_skew`
- `enrich_audience_gender_skew`
- `enrich_audience_lsm_range`
- `enrich_audience_racial_skew`
- `enrich_urban_rural_mix`
- `enrich_audience_keywords`
- `enrich_buying_behaviour_fit`
- `enrich_price_positioning_fit`
- `enrich_sales_model_fit`
- `enrich_objective_fit_primary`
- `enrich_objective_fit_secondary`
- `enrich_environment_type`
- `enrich_premium_mass_fit`
- `enrich_data_confidence`
- `enrich_notes`

Recommended formatting:
- Use short, consistent labels.
- For multi-value fields, separate values with ` | `.
- Keep `enrich_data_confidence` simple, for example `high`, `medium`, or `low`.

Useful examples:
- `enrich_target_audience`: `urban commuters | retail shoppers`
- `enrich_audience_age_skew`: `25-44`
- `enrich_audience_gender_skew`: `mixed leaning female`
- `enrich_audience_lsm_range`: `6-9`
- `enrich_audience_keywords`: `commuters | mall shoppers | family decision-makers`
- `enrich_buying_behaviour_fit`: `convenience_driven | urgency_driven`
- `enrich_price_positioning_fit`: `mid_range | premium`
- `enrich_sales_model_fit`: `walk_ins | hybrid`
- `enrich_objective_fit_primary`: `foot_traffic`
- `enrich_objective_fit_secondary`: `awareness | promotion`
- `enrich_environment_type`: `mall | commuter corridor | office district`
- `enrich_premium_mass_fit`: `premium` or `mass_market`

Row types in the file:
- `site`: raw OOH site inventory
- `package`: OOH, radio, or TV packaged inventory
- `slot`: radio or TV rate-card / slot inventory

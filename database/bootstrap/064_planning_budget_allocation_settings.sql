delete from planning_engine_settings
where setting_key = 'allocation_channel_rules_json';

insert into planning_engine_settings (setting_key, setting_value, description)
values
(
    'allocation_budget_band_rules_json',
    $$[
      {
        "name": "20k-100k",
        "min": 20000,
        "max": 100000,
        "oohTarget": 0.45,
        "billboardShareOfOoh": 0.70,
        "tvMin": 0.0,
        "tvEligible": false,
        "radioRange": [0.30, 0.35],
        "digitalRange": [0.20, 0.25]
      },
      {
        "name": "100k-500k",
        "min": 100000,
        "max": 500000,
        "oohTarget": 0.42,
        "billboardShareOfOoh": 0.65,
        "tvMin": 0.08,
        "tvEligible": true,
        "radioRange": [0.25, 0.30],
        "digitalRange": [0.20, 0.25]
      },
      {
        "name": "500k-1M",
        "min": 500000,
        "max": 1000000,
        "oohTarget": 0.38,
        "billboardShareOfOoh": 0.60,
        "tvMin": 0.20,
        "tvEligible": true,
        "radioRange": [0.20, 0.25],
        "digitalRange": [0.15, 0.20]
      },
      {
        "name": "1M-5M",
        "min": 1000000,
        "max": 5000000,
        "oohTarget": 0.35,
        "billboardShareOfOoh": 0.55,
        "tvMin": 0.28,
        "tvEligible": true,
        "radioRange": [0.15, 0.20],
        "digitalRange": [0.15, 0.20]
      }
    ]$$,
    'Planning allocation budget bands. Operators can tune Billboard, Digital Screen, TV, radio, and digital distribution by budget band without a deployment.'
),
(
    'allocation_global_rules_json',
    $${
      "maxOoh": 0.50,
      "minDigital": 0.15,
      "enforceTvFloorIfPreferred": true
    }$$,
    'Planning allocation global rules. Operators can cap billboard and digital screen share together, set a digital floor, and require a TV floor when TV is preferred.'
),
(
    'allocation_geo_rules_json',
    $$[
      {
        "policyKey": "geo_local_premium_awareness",
        "priority": 220,
        "objective": "awareness",
        "audienceSegment": "premium",
        "geographyScope": "local",
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 10,
        "weights": {
          "origin": 0.65,
          "nearby": 0.35
        }
      },
      {
        "policyKey": "geo_provincial_premium_awareness",
        "priority": 210,
        "objective": "awareness",
        "audienceSegment": "premium",
        "geographyScope": "provincial",
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 20,
        "weights": {
          "origin": 0.50,
          "nearby": 0.25,
          "wider": 0.25
        }
      },
      {
        "policyKey": "geo_provincial_general_awareness",
        "priority": 200,
        "objective": "awareness",
        "audienceSegment": "general",
        "geographyScope": "provincial",
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 20,
        "weights": {
          "origin": 0.40,
          "nearby": 0.20,
          "wider": 0.40
        }
      },
      {
        "policyKey": "geo_national_premium_awareness",
        "priority": 190,
        "objective": "awareness",
        "audienceSegment": "premium",
        "geographyScope": "national",
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 25,
        "weights": {
          "origin": 0.40,
          "nearby": 0.20,
          "wider": 0.40
        }
      },
      {
        "policyKey": "geo_launch_default",
        "priority": 180,
        "objective": "launch",
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 15,
        "weights": {
          "origin": 0.55,
          "nearby": 0.25,
          "wider": 0.20
        }
      },
      {
        "policyKey": "geo_leads_default",
        "priority": 170,
        "objective": "leads",
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 15,
        "weights": {
          "origin": 0.60,
          "nearby": 0.25,
          "wider": 0.15
        }
      },
      {
        "policyKey": "geo_default",
        "priority": 100,
        "objective": null,
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "nearbyRadiusKm": 20,
        "weights": {
          "origin": 0.50,
          "nearby": 0.25,
          "wider": 0.25
        }
      }
    ]$$,
    'Planning allocation geo rules. Operators can tune how origin, nearby coverage, and wider coverage share budget by scope, audience, and objective.'
)
on conflict (setting_key) do update
set
    setting_value = excluded.setting_value,
    description = excluded.description,
    updated_at = now();

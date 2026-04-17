insert into planning_engine_settings (setting_key, setting_value, description)
values
(
    'allocation_channel_rules_json',
    $$[
      {
        "policyKey": "channel_awareness_premium_sub50k",
        "priority": 200,
        "objective": "awareness",
        "audienceSegment": "premium",
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": 49999.99,
        "weights": {
          "ooh": 0.35,
          "radio": 0.25,
          "digital": 0.40,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_awareness_premium_50k_plus",
        "priority": 190,
        "objective": "awareness",
        "audienceSegment": "premium",
        "geographyScope": null,
        "minBudget": 50000,
        "maxBudget": null,
        "weights": {
          "ooh": 0.45,
          "radio": 0.30,
          "digital": 0.25,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_awareness_general_sub50k",
        "priority": 180,
        "objective": "awareness",
        "audienceSegment": "general",
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": 49999.99,
        "weights": {
          "ooh": 0.30,
          "radio": 0.25,
          "digital": 0.45,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_awareness_general_50k_plus",
        "priority": 170,
        "objective": "awareness",
        "audienceSegment": "general",
        "geographyScope": null,
        "minBudget": 50000,
        "maxBudget": null,
        "weights": {
          "ooh": 0.40,
          "radio": 0.30,
          "digital": 0.30,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_launch_default",
        "priority": 160,
        "objective": "launch",
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "weights": {
          "ooh": 0.40,
          "radio": 0.35,
          "digital": 0.25,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_leads_default",
        "priority": 150,
        "objective": "leads",
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "weights": {
          "ooh": 0.15,
          "radio": 0.25,
          "digital": 0.60,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_foot_traffic_default",
        "priority": 140,
        "objective": "foot_traffic",
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "weights": {
          "ooh": 0.50,
          "radio": 0.25,
          "digital": 0.25,
          "tv": 0.00
        }
      },
      {
        "policyKey": "channel_default",
        "priority": 100,
        "objective": null,
        "audienceSegment": null,
        "geographyScope": null,
        "minBudget": 0,
        "maxBudget": null,
        "weights": {
          "ooh": 0.35,
          "radio": 0.30,
          "digital": 0.35,
          "tv": 0.00
        }
      }
    ]$$,
    'Planning allocation channel rules. Operators can tune weights by objective, audience segment, scope, and budget band without a deployment.'
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

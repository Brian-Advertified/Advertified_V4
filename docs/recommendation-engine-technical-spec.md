# Recommendation Engine Technical Spec

## Purpose

This document describes the current Advertified recommendation engine, the database structures it depends on, the main instability points in the current implementation, and the target architecture required to make the engine reliable and maintainable.

This spec is intentionally grounded in the current codebase, not a hypothetical redesign.

## Scope

This spec covers the campaign recommendation engine that turns:

- campaign brief data
- package/budget data
- inventory data
- policy settings

into:

- recommendation variants
- recommendation line items
- proposal PDFs
- client approval/payment workflow state

This spec also includes the inventory and database structures required by the engine.

This spec does **not** treat lead intelligence as the same engine. Lead intelligence is a separate pipeline and is described only where it touches the recommendation flow.

## Engine Boundaries

### In Scope

- campaign brief capture and normalization
- planning request construction
- inventory sourcing
- eligibility and policy filtering
- scoring
- plan assembly
- explanation/rationale generation
- recommendation persistence
- proposal document generation
- approval workflow
- inventory persistence for OOH, radio, TV, and social

### Adjacent but Separate

- lead intelligence ingestion and scoring
- creative generation pipeline
- payment processing providers
- client registration/authentication

## Current Runtime Architecture

### Primary Services

The engine runtime is wired in `src/Advertified.App/Program.cs`.

Core services:

- `CampaignBriefService`
- `CampaignRecommendationService`
- `MediaPlanningEngine`
- `PlanningCandidateLoader`
- `PlanningInventoryRepository`
- `PlanningEligibilityService`
- `PlanningScoreService`
- `RecommendationPlanBuilder`
- `RecommendationExplainabilityService`
- `RecommendationDocumentService`
- `RecommendationApprovalWorkflowService`

Inventory sources:

- `OohPlanningInventorySource`
- `BroadcastPlanningInventorySource`
- `SocialPlanningInventorySource`

Supporting services:

- `PlanningPolicyService`
- `PlanningPolicySnapshotProvider`
- `BroadcastInventoryImportService`
- `BroadcastInventoryCatalog`
- `CampaignReasoningService` via `OpenAICampaignReasoningService`

## End-to-End Runtime Flow

### 1. Brief Capture

Client or agent brief data is saved into `campaign_briefs`.

Service:

- `CampaignBriefService`

Responsibilities:

- validate request
- normalize geography scope
- serialize list-based fields into JSON columns
- save/update the canonical brief
- save a draft copy in `campaign_brief_drafts`
- move campaign state to `brief_in_progress` or `brief_submitted`

### 2. Recommendation Request Creation

The planning request is built by:

- `CampaignRecommendationService.BuildRequest(...)`

Inputs:

- `campaigns`
- `package_orders`
- `package_band_profiles`
- `campaign_briefs`
- optional explicit mix overrides from `GenerateRecommendationRequest`

Key behaviors:

- planning budget is derived from package order amount minus AI reserve policy
- geography is normalized to `local`, `provincial`, or `national`
- target interests are augmented with strategy-derived audience terms
- target LSM can be inferred from strategy data if not explicitly provided

### 3. Proposal Variant Construction

The engine does not generate only one plan by default.

`CampaignRecommendationService` creates proposal variants:

- `balanced`
- `ooh_focus`
- either `radio_focus` or `digital_focus`

If the request already specifies an explicit channel mix, the engine uses only that requested mix.

### 4. Candidate Loading

`MediaPlanningEngine.GenerateAsync(...)` calls:

- `PlanningCandidateLoader.LoadCandidatesAsync(...)`

The candidate loader reads from:

- OOH candidate source
- broadcast candidate source
- social candidate source

Loaded candidate families:

- OOH
- radio slot rows
- radio package rows
- TV candidates
- digital/social benchmark candidates

### 5. Eligibility and Policy Filtering

`PlanningEligibilityService` applies hard filters and policy constraints before scoring.

This is where package and policy rules should be enforced consistently.

The engine also uses:

- `PlanningPolicyService`
- `PlanningPolicySnapshotProvider`

Current policy examples:

- minimum radio rules for higher budget packages
- national-capable radio preference
- repeatability rules for some candidate types
- requested target-share interpretation

### 6. Scoring

`PlanningScoreService` scores each candidate.

Current scoring dimensions include:

- geography fit
- audience fit
- language fit
- budget fit
- media preference fit
- objective fit
- strategy fit
- availability
- OOH priority
- requested target mix support
- radio-specific bonuses

Important recent behavior:

- broadcast language now matters more strongly than before
- radio/TV language matches can materially lift ranking
- radio/TV language mismatches can now penalize ranking

### 7. Explanation Layer

`RecommendationExplainabilityService` produces:

- selection reasons
- policy flags
- confidence score
- stored rationale text

This layer does not choose candidates. It interprets the chosen candidates and the request.

### 8. Plan Assembly

`RecommendationPlanBuilder` builds:

- base plan
- recommended plan
- upsells

Behaviors:

- budget-constrained selection
- optional diversification mode
- target-mix-aware plan building
- budget gap filling
- exact-fill attempt for smaller candidate pools
- limited repeat handling for repeatable inventory

### 9. Recommendation Persistence

`CampaignRecommendationService.GenerateAndSaveAsync(...)` stores:

- one or more recommendation rows
- recommendation items for each selected line
- rationale and summary
- revision number

Tables:

- `campaign_recommendations`
- `campaign_recommendation_items`

### 10. Proposal Document Generation

`RecommendationDocumentService` turns saved recommendations into:

- proposal document payloads
- PDF snapshots
- proposal links with access tokens

### 11. Approval and Payment Progression

`RecommendationApprovalWorkflowService` handles:

- client approval
- payment gate enforcement
- recommendation status updates
- email lifecycle hooks

## Current Canonical Data Sources

This is the most important section for stabilization.

### Canonical Source of Truth by Domain

#### Campaign Strategy Input

Canonical source:

- `campaign_briefs`

Do not duplicate this in parallel form models or shadow tables.

#### Recommendation Output

Canonical sources:

- `campaign_recommendations`
- `campaign_recommendation_items`

#### Broadcast Inventory Catalog

Canonical source today:

- `src/Advertified.App/App_Data/broadcast/enriched_broadcast_inventory_normalized.json`

This is critical:

- `radio` and `tv` rows in `media_outlet` are not the master source
- `BroadcastInventoryImportService` deletes and rebuilds broadcast rows from the JSON source

So direct DB edits to broadcast inventory are temporary unless the JSON source is also updated.

#### OOH Inventory

Canonical source today:

- Postgres inventory rows seeded and maintained in database tables
- seeded by bootstrap SQL such as `024_seed_ooh_baseline.sql`

#### Social Inventory

Canonical source today:

- SQL seed path into the unified `media_outlet` tables
- currently via `038_social_inventory_seed.sql`

## Database Model

## Core Campaign Tables

### `campaigns`

Role:

- top-level campaign lifecycle record

Key responsibilities:

- current campaign status
- package linkage
- user linkage
- planning mode
- assignment/routing
- proposal/payment stage visibility

### `package_orders`

Role:

- payment/commercial order for the package backing the campaign

Key fields used by the engine:

- `selected_budget`
- `amount`
- `payment_provider`
- `payment_status`
- `ai_studio_reserve_percent`
- `ai_studio_reserve_amount`

Why it matters:

- planning budget is derived from this record
- client state and approval/payment progression depend on this record

### `campaign_briefs`

Role:

- canonical strategic input for the recommendation engine

Key fields:

- `objective`
- `business_stage`
- `monthly_revenue_band`
- `sales_model`
- `geography_scope`
- `provinces_json`
- `cities_json`
- `suburbs_json`
- `areas_json`
- `target_age_min`
- `target_age_max`
- `target_gender`
- `target_languages_json`
- `target_lsm_min`
- `target_lsm_max`
- `target_interests_json`
- `target_audience_notes`
- `customer_type`
- `buying_behaviour`
- `decision_cycle`
- `price_positioning`
- `average_customer_spend_band`
- `growth_target`
- `urgency_level`
- `audience_clarity`
- `value_proposition_focus`
- `preferred_media_types_json`
- `excluded_media_types_json`
- `must_have_areas_json`
- `excluded_areas_json`
- `creative_ready`
- `creative_notes`
- `max_media_items`
- `open_to_upsell`
- `additional_budget`
- `special_requirements`
- video preference fields

Design note:

- this is already the correct source-of-truth table for strategy input
- engine cleanup should reduce duplication around this table, not add new parallel tables

### `campaign_brief_drafts`

Role:

- recovery/autosave copy for in-progress brief completion

Not a planning source of truth.

## Recommendation Tables

### `campaign_recommendations`

Role:

- persisted recommendation headers / proposal variants

Key fields:

- `campaign_id`
- `recommendation_type`
- `generated_by`
- `status`
- `summary`
- `rationale`
- `total_cost`
- `revision_number`
- `sent_to_client_at`
- `approved_at`
- PDF storage columns

### `campaign_recommendation_items`

Role:

- line items for selected plan rows

Expected contents:

- source IDs
- source type
- media type
- cost/quantity
- item metadata
- whether line is upsell

This table is where recommendation reproducibility either succeeds or fails.

If metadata stored here is too thin, later document generation and booking become fragile.

## Inventory Tables

### Unified Inventory Catalog

Broadcast and social use the unified outlet model:

- `media_outlet`
- `media_outlet_keyword`
- `media_outlet_language`
- `media_outlet_geography`
- `media_outlet_pricing_package`
- `media_outlet_slot_rate`

### `media_outlet`

Role:

- outlet/channel-level record

Important fields:

- `code`
- `name`
- `media_type`
- `coverage_type`
- `catalog_health`
- `is_national`
- `has_pricing`
- `language_notes`
- audience metadata
- `broadcast_frequency`
- listenership fields
- `target_audience`
- `data_source_enrichment`

### `media_outlet_language`

Role:

- explicit primary and secondary language support per outlet

This is now essential for language-led broadcast ranking.

### `media_outlet_geography`

Role:

- province and city targeting metadata for outlets

Needed by:

- geography eligibility
- geography scoring
- explainability

### `media_outlet_pricing_package`

Role:

- package-style pricing rows

Used for:

- broadcast package candidates
- social benchmark package candidates
- OOH package-style inventory where applicable

### `media_outlet_slot_rate`

Role:

- row-level spot/slot pricing

Used primarily for:

- radio slot candidates
- TV slot candidates where available

### OOH Legacy/Parallel Inventory

OOH still also depends on legacy/finalized inventory structures outside the broadcast tables, including:

- `inventory_items_final`
- related media import pipeline tables

This is one of the major architecture inconsistencies in the current engine.

## Policy and Configuration Tables

### `admin_engine_policy_overrides`

Role:

- package-level planning rule overrides

Fields:

- `package_code`
- `budget_floor`
- `minimum_national_radio_candidates`
- `require_national_capable_radio`
- `require_premium_national_radio`
- radio bonus/penalty controls

### `pricing_settings`

Role:

- pricing/markup rules used by inventory and package calculations

Not a scorer table, but it affects planning economics.

## Prospect / Conversion Tables

### `prospect_leads`

Role:

- pre-account prospect records

Important because it decouples lead capture from forced user creation.

### `campaigns.prospect_lead_id`
### `package_orders.prospect_lead_id`

Role:

- links campaign/order objects to a lead-first workflow

## Lead Intelligence Tables

These are related but separate from the recommendation engine:

- `leads`
- `signals`
- `lead_insights`
- `lead_actions`
- `lead_interactions`

Use these for top-of-funnel intelligence, not recommendation planning.

## Inventory Ingestion and Refresh Behavior

## Broadcast Import Behavior

`BroadcastInventoryImportService.SyncAsync(...)` currently:

1. reads `enriched_broadcast_inventory_normalized.json`
2. deletes all `radio` and `tv` rows from `media_outlet`
3. recreates broadcast outlet, keyword, language, geography, package, and slot-rate rows
4. refreshes the in-memory catalog

Implication:

- broadcast DB edits are ephemeral unless the JSON source is updated too

This is the single most important operational rule for radio/TV.

## Current Engine Weak Points

These are the areas most likely causing the repeated engine frustration.

### 1. Split Inventory Architecture

Current state:

- OOH uses one data path
- broadcast uses JSON -> sync -> unified outlet tables
- social uses direct SQL seeding into unified outlet tables

Problem:

- three operational patterns for one engine

Impact:

- hard to know what the real source of truth is
- easier to fix one channel family and accidentally regress another

### 2. Broadcast Sync is Destructive

Current state:

- broadcast sync deletes and rebuilds all radio/TV rows

Problem:

- direct DB fixes do not persist
- admins can believe they fixed data when the next sync wipes it

### 3. Strategy Input Has Been Historically Duplicated in UI

Current state:

- backend canonical table is correct
- frontend has had multiple form models around the same brief

Problem:

- drift between questionnaire, client brief, and agent recommendation form

### 4. Policy Logic is Distributed

Current state:

- policy lives across:
  - `PlanningEligibilityService`
  - `PlanningPolicyService`
  - `RecommendationPlanBuilder`
  - package/profile assumptions in request construction

Problem:

- business rules are harder to reason about end to end

### 5. Recommendation Status and Payment State Have Historically Leaked Into Each Other

Current state:

- recommendation approval
- payment required
- pay-later submitted
- pay-later under review

have not always been modeled from one shared state source.

Impact:

- confusing client messaging
- inconsistent approval/payment behavior

### 6. Explainability Depends on Candidate Metadata Quality

Current state:

- selection reasons and confidence are metadata-sensitive

Problem:

- if inventory metadata is incomplete, explainability becomes noisy or misleading even when the selected line is commercially reasonable

## Three Engine Test Questions

The recommendation engine is only strong if the answer is yes to all three of these questions.

### 1. Can We Prove Why It Chose This?

Meaning:

- the engine decision is inspectable, not mystical
- a human should not need to read code to explain a recommendation

For any recommendation run, the system should be able to show:

- the normalized request snapshot
- candidate counts by channel
- which candidates were rejected
- why they were rejected
- how remaining candidates were scored
- which policy rules affected eligibility or ranking
- why the final set was assembled the way it was

Required engine capabilities:

- run audit logs
- rejection reasons
- score breakdowns
- policy snapshots
- candidate counts by channel and filter stage

Current answer:

- partially

Why it is not yet a full yes:

- policy logic is still distributed
- explainability still depends too heavily on metadata quality
- run observability is too weak for reliable operator-facing answers

### 2. Can We Reproduce It Later?

Meaning:

- a recommendation is a frozen artifact, not a temporary view over mutable tables
- the same proposal should be reconstructable later without guessing

To satisfy this, the system must persist:

- the normalized request snapshot
- the policy snapshot used during the run
- the inventory version or import batch used during the run
- enough immutable recommendation item metadata to rebuild the proposal without rereading changed source rows

Current answer:

- not reliably enough

Why it is not yet a full yes:

- reproducibility currently succeeds or fails based on how much metadata is stored in `campaign_recommendation_items`
- later document generation and booking can still depend on mutable source inventory
- policy and inventory versioning are not yet strong enough to guarantee replay

### 3. Can We Safely Change Inventory Without Silent Breakage?

Meaning:

- inventory changes are controlled, traceable, and reversible
- runtime changes do not quietly disappear

To satisfy this, the system must provide:

- import batches or versions for every inventory change
- validation before a batch becomes active
- comparison and rollback capability
- clear admin-facing source-of-truth rules
- recommendation-to-inventory-batch traceability

Current answer:

- no, especially for broadcast

Why it is not yet a yes:

- broadcast sync currently deletes and rebuilds runtime rows
- direct DB fixes can silently disappear on the next sync
- runtime output changes are not yet tied cleanly to an immutable inventory version

## What These Questions Actually Measure

These three questions map to the actual engine properties that matter most:

- prove why = observability
- reproduce later = persistence plus versioning
- safely change inventory = controlled ingestion

This is why the next stabilization work should not focus mainly on "better scoring" first.

The current architecture is already good enough to support stronger decision-making, but the main fragility is in:

- governance
- runtime guarantees
- operational traceability

## Current Pass/Fail Summary

Based on the current implementation described in this spec:

- prove why: partially
- reproduce later: not reliably enough
- safely change inventory: no, especially for broadcast

If any answer remains no, the engine should be treated as not yet stabilized.

## Target Stabilized Architecture

This is the recommended end state.

### 1. One Canonical Strategic Input Model

Keep:

- `campaign_briefs` as the single source of truth

Do:

- ensure every UI flow maps into the same backend brief shape
- eliminate UI-specific shadow interpretations where possible

### 2. One Canonical Recommendation Persistence Model

Keep:

- `campaign_recommendations`
- `campaign_recommendation_items`

Do:

- ensure every selected line stores enough metadata to reconstruct proposal, booking, and review history without re-deriving from volatile inventory rows
- persist the normalized request snapshot, policy snapshot, and inventory version used for the run

### 3. One Canonical Inventory Ingestion Contract Per Channel Family

Recommended target:

- keep unified outlet tables as runtime inventory substrate
- replace destructive broadcast "delete and rebuild" with versioned upsert-based synchronization

Minimum acceptable interim rule:

- broadcast JSON remains the master source until a proper admin-managed ingestion workflow replaces it

### 4. One Shared Policy Layer

Recommended target:

- all hard eligibility rules in one layer
- all scoring weight rules in one layer
- all plan-construction heuristics in one layer
- package and budget policy data stored/configured centrally

### 5. One Explicit Customer State Model

Recommendation/payment/client workspace behavior must read from one derived state model, not page-specific interpretations.

### 6. One Observable Recommendation Run Record

Every recommendation run should persist enough information to answer why the engine chose what it chose.

Minimum required contents:

- normalized request snapshot
- candidate counts by channel
- rejection reasons by stage
- score breakdowns for selected lines
- policy snapshot
- fallback flags
- budget utilization summary
- final selection rationale

## Recommended Refactor Plan

### Phase 1: Freeze Sources of Truth

1. Declare canonical sources in code comments and docs.
2. Stop direct admin-side edits to broadcast runtime rows unless they update the source JSON/import artifact too.
3. Add source-of-truth warnings to admin tooling for broadcast.

### Phase 2: Normalize Inventory Runtime

1. Move toward a single ingestion contract for all channels.
2. Introduce explicit inventory versioning or import batches.
3. Replace destructive broadcast rebuild with idempotent upsert logic.

### Phase 3: Consolidate Policy

1. Document each policy rule and its current owner.
2. Move duplicated rules into `PlanningPolicyService` or a dedicated rule layer.
3. Make plan builder consume policy outputs rather than rediscovering rules.

### Phase 4: Strengthen Recommendation Persistence

1. Ensure recommendation items store immutable line metadata required for later workflows.
2. Persist request snapshot, policy snapshot, and inventory version references with the recommendation revision.
3. Reduce dependence on re-reading mutable source inventory when rendering historic recommendations.

### Phase 5: Add Observability

For every engine run, persist or emit:

- normalized request snapshot
- candidate counts by channel
- candidate counts by filter stage
- rejection reasons
- score breakdowns for selected lines
- policy snapshot
- fallback flags
- final selected channels
- budget utilization
- manual review reasons

Without this, engine debugging remains too manual.

## Recommended Database Improvements

### High Priority

1. Add explicit inventory import batch/version tables.
2. Add recommendation-run audit/log table.
3. Add normalized channel enum or controlled mapping layer to reduce `OOH/ooh/Billboard` drift.
4. Add explicit persisted engine policy snapshot per recommendation revision.
5. Add request snapshot and inventory-batch references to recommendation revisions.

### Medium Priority

1. Separate runtime-ready inventory from enrichment-only catalog rows more explicitly.
2. Add data quality status columns that reflect real readiness:
   - metadata completeness
   - pricing completeness
   - engine readiness
   - booking readiness

### Low Priority

1. Replace JSON arrays in some brief fields with normalized tables only if reporting/querying truly demands it.
2. Do not normalize prematurely if it creates more drift than it solves.

## Acceptance Criteria for "Engine Fixed Once and for All"

The engine should only be considered stabilized when:

1. Every campaign brief flow writes to one canonical brief shape.
2. Every recommendation run stores enough data to prove why the engine chose the final plan.
3. Every recommendation is reproducible from persisted recommendation data plus versioned inventory and policy sources.
4. Broadcast, OOH, and social inventory all have a documented source-of-truth contract.
5. Inventory updates do not silently disappear on restart.
6. Language, geography, and pricing are trustworthy across all live recommendation channels.
7. Client, payment, and recommendation states are derived from one shared model.
8. Engine runs can be audited when a recommendation looks wrong.
9. Recommendations can be tied back to the exact inventory batch and policy snapshot used at generation time.

## Immediate Fix List

If you want the fastest high-value cleanup path, do these next:

1. Add this spec to the repo and treat it as the engine contract.
2. Add a recommendation-run audit table that captures normalized request, candidate counts, rejection reasons, score breakdowns, and final selection rationale.
3. Introduce an `inventory_import_batches` model and stop blind destructive broadcast replacement.
4. Persist policy snapshots and inventory-batch references with each recommendation revision.
5. Centralize remaining policy drift.
6. Finish broadcast pricing completeness and align runtime health statuses to real engine readiness.

## Related Source Files

Primary engine code:

- `src/Advertified.App/Services/MediaPlanningEngine.cs`
- `src/Advertified.App/Services/CampaignRecommendationService.cs`
- `src/Advertified.App/Services/PlanningCandidateLoader.cs`
- `src/Advertified.App/Services/PlanningInventoryRepository.cs`
- `src/Advertified.App/Services/PlanningScoreService.cs`
- `src/Advertified.App/Services/RecommendationPlanBuilder.cs`
- `src/Advertified.App/Services/RecommendationExplainabilityService.cs`
- `src/Advertified.App/Services/BroadcastInventoryImportService.cs`

Core schema/config files:

- `database/bootstrap/008_broadcast_inventory_v2.sql`
- `database/bootstrap/012_admin_engine_policy_overrides.sql`
- `database/bootstrap/034_campaign_brief_strategy_intelligence.sql`
- `database/bootstrap/036_inventory_strategy_intelligence.sql`
- `database/bootstrap/037_prospect_leads.sql`
- `database/bootstrap/038_social_inventory_seed.sql`

Campaign persistence:

- `src/Advertified.App/Data/AppDbContext.cs`
- `src/Advertified.App/Data/AppDbContext.Partial.cs`

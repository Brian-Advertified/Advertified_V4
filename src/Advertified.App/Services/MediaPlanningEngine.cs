using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class MediaPlanningEngine : IMediaPlanningEngine
{
    private readonly IPlanningInventoryRepository _repository;
    private readonly PlanningPolicyOptions _policyOptions;

    public MediaPlanningEngine(
        IPlanningInventoryRepository repository,
        IOptions<PlanningPolicyOptions> policyOptions)
    {
        _repository = repository;
        _policyOptions = policyOptions.Value;
    }

    public async Task<RecommendationResult> GenerateAsync(
        CampaignPlanningRequest request,
        CancellationToken cancellationToken)
    {
        var allCandidates = await LoadCandidatesAsync(request, cancellationToken);
        var policyOutcome = FilterEligibleCandidates(allCandidates, request);
        var eligibleCandidates = policyOutcome.Candidates;

        foreach (var candidate in eligibleCandidates)
        {
            var analysis = AnalyzeCandidate(candidate, request);
            candidate.Score = analysis.Score;
            candidate.Metadata["selectionReasons"] = analysis.SelectionReasons;
            candidate.Metadata["policyFlags"] = analysis.PolicyFlags;
            candidate.Metadata["confidenceScore"] = analysis.ConfidenceScore;
        }

        var scored = eligibleCandidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Cost)
            .ToList();

        var basePlan = BuildPlan(
            scored,
            request.SelectedBudget,
            request.MaxMediaItems,
            diversify: true);

        var recommendedPlan = BuildPlan(
            scored,
            request.SelectedBudget,
            request.MaxMediaItems,
            diversify: false);

        var upsellBudget = request.OpenToUpsell
            ? request.SelectedBudget + (request.AdditionalBudget ?? 0m)
            : request.SelectedBudget;

        var upsells = request.OpenToUpsell && upsellBudget > request.SelectedBudget
            ? BuildUpsells(scored, recommendedPlan, upsellBudget - recommendedPlan.Sum(x => x.TotalCost))
            : new List<PlannedItem>();

        var fallbackFlags = new List<string>(policyOutcome.FallbackFlags);
        if (eligibleCandidates.Count == 0)
        {
            fallbackFlags.Add("inventory_insufficient");
        }

        if (recommendedPlan.Count == 0)
        {
            fallbackFlags.Add("no_recommendation_generated");
        }

        fallbackFlags.AddRange(GetPreferredMediaFallbackFlags(request, recommendedPlan));

        return new RecommendationResult
        {
            BasePlan = basePlan,
            RecommendedPlan = recommendedPlan,
            Upsells = upsells,
            FallbackFlags = fallbackFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ManualReviewRequired = fallbackFlags.Count > 0,
            Rationale = BuildRationale(basePlan, recommendedPlan, request)
        };
    }

    private async Task<List<InventoryCandidate>> LoadCandidatesAsync(
        CampaignPlanningRequest request,
        CancellationToken cancellationToken)
    {
        var ooh = await _repository.GetOohCandidatesAsync(request, cancellationToken);
        var radioSlots = await _repository.GetRadioSlotCandidatesAsync(request, cancellationToken);
        var radioPackages = await _repository.GetRadioPackageCandidatesAsync(request, cancellationToken);

        return ooh
            .Concat(radioSlots)
            .Concat(radioPackages)
            .ToList();
    }

    private PolicyOutcome FilterEligibleCandidates(
        List<InventoryCandidate> candidates,
        CampaignPlanningRequest request)
    {
        var eligible = candidates
            .Where(candidate => IsEligibleCandidate(candidate, request))
            .ToList();

        return ApplyHigherBandRadioEligibility(eligible, request);
    }

    private static bool IsEligibleCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.IsAvailable)
        {
            return false;
        }

        if (candidate.Cost <= 0 || candidate.Cost > request.SelectedBudget)
        {
            return false;
        }

        if (request.ExcludedMediaTypes.Contains(candidate.MediaType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!MatchesPreferredMedia(candidate, request))
        {
            return false;
        }

        if (!MatchesRequestedGeography(candidate, request))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesPreferredMedia(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.PreferredMediaTypes.Count == 0)
        {
            return true;
        }

        return request.PreferredMediaTypes.Any(preferred =>
            Matches(preferred, candidate.MediaType)
            || Matches(preferred, candidate.Subtype));
    }

    private static bool MatchesRequestedGeography(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var hasSpecificGeography = request.Suburbs.Count > 0
            || request.Cities.Count > 0
            || request.Provinces.Count > 0
            || request.Areas.Count > 0;

        if (!hasSpecificGeography)
        {
            return true;
        }

        if (request.Suburbs.Any(x => Matches(x, candidate.Suburb) || Matches(x, candidate.Area)))
        {
            return true;
        }

        if (request.Cities.Any(x => Matches(x, candidate.City)))
        {
            return true;
        }

        if (request.Provinces.Any(x => Matches(x, candidate.Province)))
        {
            return true;
        }

        if (request.Areas.Any(x => Matches(x, candidate.Area) || Matches(x, candidate.Suburb)))
        {
            return true;
        }

        return Matches(request.GeographyScope, candidate.MarketScope)
            || Matches(request.GeographyScope, candidate.RegionClusterCode);
    }

    private decimal ScoreCandidate(InventoryCandidate c, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        score += GeoScore(c, request);
        score += AudienceScore(c, request);
        score += BudgetScore(c, request);
        score += MediaPreferenceScore(c, request);
        score += AvailabilityScore(c);
        score += OohPriorityScore(c, request);

        if (c.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            score += RadioFitBonus(c, request);
        }

        return score;
    }

    private CandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var score = ScoreCandidate(candidate, request);
        var selectionReasons = BuildSelectionReasons(candidate, request);
        var policyFlags = BuildPolicyFlags(candidate, request);
        var confidenceScore = CalculateConfidenceScore(candidate, request);

        return new CandidateAnalysis(score, selectionReasons, policyFlags, confidenceScore);
    }

    private static decimal GeoScore(InventoryCandidate c, CampaignPlanningRequest request)
    {
        if (request.Suburbs.Any(x => Matches(x, c.Suburb) || Matches(x, c.Area))) return 30m;
        if (request.Cities.Any(x => Matches(x, c.City))) return 24m;
        if (request.Provinces.Any(x => Matches(x, c.Province))) return 16m;
        if (request.Areas.Any(x => Matches(x, c.Area) || Matches(x, c.Suburb))) return 22m;
        return 4m;
    }

    private static decimal AudienceScore(InventoryCandidate c, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        if (request.TargetLanguages.Count > 0 && !string.IsNullOrWhiteSpace(c.Language))
        {
            if (request.TargetLanguages.Any(x => Matches(x, c.Language)))
            {
                score += 10m;
            }
        }

        if (request.TargetLsmMin.HasValue && request.TargetLsmMax.HasValue && c.LsmMin.HasValue && c.LsmMax.HasValue)
        {
            var overlap = !(c.LsmMax.Value < request.TargetLsmMin.Value || c.LsmMin.Value > request.TargetLsmMax.Value);
            if (overlap)
            {
                score += 15m;
            }
        }

        return score;
    }

    private static decimal BudgetScore(InventoryCandidate c, CampaignPlanningRequest request)
    {
        if (c.Cost <= 0 || request.SelectedBudget <= 0)
        {
            return 0m;
        }

        var ratio = c.Cost / request.SelectedBudget;

        if (ratio <= 0.15m) return 20m;
        if (ratio <= 0.30m) return 16m;
        if (ratio <= 0.50m) return 12m;
        if (ratio <= 0.80m) return 8m;
        if (ratio <= 1.00m) return 4m;
        return 0m;
    }

    private static decimal MediaPreferenceScore(InventoryCandidate c, CampaignPlanningRequest request)
    {
        if (request.PreferredMediaTypes.Count == 0) return 6m;
        return request.PreferredMediaTypes.Any(x => Matches(x, c.MediaType) || Matches(x, c.Subtype))
            ? 15m
            : 0m;
    }

    private static decimal AvailabilityScore(InventoryCandidate c)
    {
        return c.IsAvailable ? 10m : 0m;
    }

    private static decimal OohPriorityScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var preferredOoh = request.PreferredMediaTypes.Any(preferred =>
            Matches(preferred, "ooh") || Matches(preferred, candidate.MediaType) || Matches(preferred, candidate.Subtype));

        return preferredOoh ? 30m : 18m;
    }

    private decimal RadioFitBonus(InventoryCandidate c, CampaignPlanningRequest request)
    {
        decimal bonus = 0m;

        if (!string.IsNullOrWhiteSpace(c.TimeBand))
        {
            if (Matches(c.TimeBand, "breakfast") || Matches(c.TimeBand, "drive"))
            {
                bonus += 8m;
            }
            else
            {
                bonus += 4m;
            }
        }

        if (!string.IsNullOrWhiteSpace(c.DayType) && Matches(c.DayType, "weekday"))
        {
            bonus += 3m;
        }

        if (!string.IsNullOrWhiteSpace(c.SlotType) && Matches(c.SlotType, "commercial"))
        {
            bonus += 4m;
        }

        bonus += GetHigherBandRadioBonus(c, request);

        return bonus;
    }

    private PolicyOutcome ApplyHigherBandRadioEligibility(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        if (request.SelectedBudget < _policyOptions.Scale.BudgetFloor)
        {
            return new PolicyOutcome(candidates, Array.Empty<string>());
        }

        var radioCandidates = candidates
            .Where(candidate => candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (radioCandidates.Count == 0)
        {
            return new PolicyOutcome(candidates, new[] { "radio_inventory_unavailable" });
        }

        var nationalRadioCandidates = radioCandidates
            .Where(candidate => IsNationalCapableRadioCandidate(candidate, request))
            .ToList();

        var applicablePolicy = request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor
            ? _policyOptions.Dominance
            : _policyOptions.Scale;
        var minimumNationalCandidates = applicablePolicy.MinimumNationalRadioCandidates;
        if (nationalRadioCandidates.Count < minimumNationalCandidates)
        {
            return new PolicyOutcome(candidates, new[] { "national_radio_inventory_insufficient", "policy_relaxed" });
        }

        return new PolicyOutcome(
            candidates
            .Where(candidate => !candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) || IsNationalCapableRadioCandidate(candidate, request))
            .ToList(),
            Array.Empty<string>());
    }

    private decimal GetHigherBandRadioBonus(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) || request.SelectedBudget < _policyOptions.Scale.BudgetFloor)
        {
            return 0m;
        }

        var isNational = IsNationalCapableRadioCandidate(candidate, request);
        var isRegionalOnly = IsRegionalOrProvincialRadioCandidate(candidate);
        var applicablePolicy = request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor
            ? _policyOptions.Dominance
            : _policyOptions.Scale;

        if (isNational)
        {
            return applicablePolicy.NationalRadioBonus;
        }

        return isRegionalOnly
            ? -applicablePolicy.RegionalRadioPenalty
            : -applicablePolicy.NonNationalRadioPenalty;
    }

    private bool IsNationalCapableRadioCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var marketScope = candidate.MarketScope?.Trim() ?? string.Empty;
        var marketTier = candidate.MarketTier?.Trim() ?? string.Empty;
        var clusterCode = candidate.RegionClusterCode?.Trim() ?? string.Empty;
        var displayName = candidate.DisplayName ?? string.Empty;

        var isNational = marketScope.Equals("national", StringComparison.OrdinalIgnoreCase)
            || clusterCode.Equals("national", StringComparison.OrdinalIgnoreCase);
        var isFlagshipOrPremium = candidate.IsFlagshipStation
            || candidate.IsPremiumStation
            || marketTier.Equals("flagship", StringComparison.OrdinalIgnoreCase)
            || marketTier.Equals("premium", StringComparison.OrdinalIgnoreCase);

        if (request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor)
        {
            return _policyOptions.Dominance.RequirePremiumNationalRadio
                ? isNational && isFlagshipOrPremium
                : isNational;
        }

        if (!_policyOptions.Scale.RequireNationalCapableRadio)
        {
            return true;
        }

        return isNational || candidate.IsFlagshipStation || displayName.Contains("Metro FM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegionalOrProvincialRadioCandidate(InventoryCandidate candidate)
    {
        var marketScope = candidate.MarketScope?.Trim() ?? string.Empty;
        var clusterCode = candidate.RegionClusterCode?.Trim() ?? string.Empty;
        var displayName = candidate.DisplayName ?? string.Empty;

        return marketScope.Equals("regional", StringComparison.OrdinalIgnoreCase)
            || clusterCode.Equals("gauteng", StringComparison.OrdinalIgnoreCase)
            || clusterCode.Equals("western-cape", StringComparison.OrdinalIgnoreCase)
            || clusterCode.Equals("eastern-cape", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("Kaya 959", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("JOZI FM", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("Smile 90.4FM", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("ALGOA FM", StringComparison.OrdinalIgnoreCase);
    }

    private static List<PlannedItem> BuildPlan(
        List<InventoryCandidate> candidates,
        decimal budget,
        int? maxItems,
        bool diversify)
    {
        var result = new List<PlannedItem>();
        var spent = 0m;
        var usedMediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (candidate.Cost <= 0) continue;
            if (spent + candidate.Cost > budget) continue;
            if (maxItems.HasValue && result.Count >= maxItems.Value) break;

            if (diversify && usedMediaTypes.Contains(candidate.MediaType))
            {
                var alreadyHasDifferentType = usedMediaTypes.Count >= 2;
                if (!alreadyHasDifferentType)
                {
                    continue;
                }
            }

            result.Add(ToPlannedItem(candidate));
            spent += candidate.Cost;
            usedMediaTypes.Add(candidate.MediaType);
        }

        FillBudgetGap(result, candidates, budget, maxItems);

        if (result.Count == 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Cost <= budget)
                {
                    result.Add(ToPlannedItem(candidate));
                    break;
                }
            }
        }

        return result;
    }

    private static void FillBudgetGap(
        List<PlannedItem> result,
        List<InventoryCandidate> candidates,
        decimal budget,
        int? maxItems)
    {
        if (result.Count == 0)
        {
            return;
        }

        var remaining = budget - result.Sum(x => x.TotalCost);
        if (remaining <= 0m)
        {
            return;
        }

        var fillCandidates = candidates
            .Where(candidate => candidate.Cost > 0m && candidate.Cost <= budget)
            .GroupBy(candidate => candidate.SourceId)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Cost)
                .First())
            .OrderByDescending(candidate => candidate.Cost)
            .ThenByDescending(candidate => candidate.Score)
            .ToList();

        if (fillCandidates.Count == 0)
        {
            return;
        }

        var maxDepth = Math.Min(6, Math.Max(2, maxItems.GetValueOrDefault(result.Count + 3)));
        var exactFill = TryBuildExactFill(fillCandidates, remaining, maxDepth, maxItems, result);

        if (exactFill.Count > 0)
        {
            foreach (var candidate in exactFill)
            {
                AddOrIncrement(result, candidate);
            }

            return;
        }

        var iterations = 0;
        while (remaining > 0m && iterations < 12)
        {
            var candidate = fillCandidates
                .Where(x =>
                    x.Cost <= remaining &&
                    (
                        (result.Any(item => item.SourceId == x.SourceId) && IsRepeatableCandidate(x))
                        || !maxItems.HasValue
                        || result.Count < maxItems.Value
                    ))
                .OrderByDescending(x => x.Cost)
                .ThenByDescending(x => x.Score)
                .FirstOrDefault();

            if (candidate is null)
            {
                break;
            }

            AddOrIncrement(result, candidate);
            remaining -= candidate.Cost;
            iterations++;
        }
    }

    private static IReadOnlyList<InventoryCandidate> TryBuildExactFill(
        IReadOnlyList<InventoryCandidate> candidates,
        decimal remaining,
        int depthRemaining,
        int? maxItems,
        IReadOnlyList<PlannedItem> currentPlan)
    {
        if (remaining == 0m)
        {
            return Array.Empty<InventoryCandidate>();
        }

        if (depthRemaining <= 0)
        {
            return Array.Empty<InventoryCandidate>();
        }

        foreach (var candidate in candidates.Where(x => x.Cost <= remaining))
        {
            var wouldAddNewLine = currentPlan.All(item => item.SourceId != candidate.SourceId);
            var wouldRepeatExisting = !wouldAddNewLine;

            if (wouldRepeatExisting && !IsRepeatableCandidate(candidate))
            {
                continue;
            }

            if (wouldAddNewLine && maxItems.HasValue && currentPlan.Count >= maxItems.Value)
            {
                continue;
            }

            if (candidate.Cost == remaining)
            {
                return new[] { candidate };
            }

            var simulatedPlan = wouldAddNewLine
                ? currentPlan.Concat(new[] { ToPlannedItem(candidate) }).ToArray()
                : currentPlan;
            var tail = TryBuildExactFill(candidates, remaining - candidate.Cost, depthRemaining - 1, maxItems, simulatedPlan);
            if (tail.Count > 0 || remaining - candidate.Cost == 0m)
            {
                return new[] { candidate }.Concat(tail).ToArray();
            }
        }

        return Array.Empty<InventoryCandidate>();
    }

    private static List<PlannedItem> BuildUpsells(
        List<InventoryCandidate> candidates,
        List<PlannedItem> recommendedPlan,
        decimal upsellHeadroom)
    {
        var selectedIds = recommendedPlan.Select(x => x.SourceId).ToHashSet();
        var result = new List<PlannedItem>();
        var spent = 0m;

        foreach (var candidate in candidates.Where(x => !selectedIds.Contains(x.SourceId)))
        {
            if (candidate.Cost <= 0) continue;
            if (spent + candidate.Cost > upsellHeadroom) continue;

            result.Add(ToPlannedItem(candidate));
            spent += candidate.Cost;

            if (result.Count >= 3) break;
        }

        return result;
    }

    private static string BuildRationale(
        List<PlannedItem> basePlan,
        List<PlannedItem> recommendedPlan,
        CampaignPlanningRequest request)
    {
        var mediaMix = string.Join(", ", recommendedPlan.Select(x => x.MediaType).Distinct());
        return $"Plan built within budget of {request.SelectedBudget:n0}, prioritising geography fit, audience fit, media preference, and available inventory. Selected mix: {mediaMix}.";
    }

    private static IReadOnlyList<string> GetPreferredMediaFallbackFlags(
        CampaignPlanningRequest request,
        List<PlannedItem> recommendedPlan)
    {
        if (request.PreferredMediaTypes.Count == 0 || recommendedPlan.Count == 0)
        {
            return Array.Empty<string>();
        }

        var selectedMedia = recommendedPlan
            .Select(item => item.MediaType.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return request.PreferredMediaTypes
            .Select(preferred => preferred.Trim().ToLowerInvariant())
            .Where(preferred => !string.IsNullOrWhiteSpace(preferred))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(preferred => !selectedMedia.Contains(preferred))
            .Select(preferred => $"preferred_media_unfulfilled:{preferred}")
            .ToArray();
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static PlannedItem ToPlannedItem(InventoryCandidate candidate)
    {
        return new PlannedItem
        {
            SourceId = candidate.SourceId,
            SourceType = candidate.SourceType,
            DisplayName = candidate.DisplayName,
            MediaType = candidate.MediaType,
            UnitCost = candidate.Cost,
            Quantity = 1,
            Score = candidate.Score,
            Metadata = new Dictionary<string, object?>(candidate.Metadata)
        };
    }

    private static void AddOrIncrement(List<PlannedItem> result, InventoryCandidate candidate)
    {
        var existing = result.FirstOrDefault(item => item.SourceId == candidate.SourceId);
        if (existing is not null)
        {
            if (!IsRepeatableCandidate(candidate))
            {
                return;
            }

            existing.Quantity += 1;
            return;
        }

        result.Add(ToPlannedItem(candidate));
    }

    private string[] BuildSelectionReasons(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var reasons = new List<string>();

        if (GeoScore(candidate, request) >= 22m)
        {
            reasons.Add("Strong geography match");
        }
        else if (GeoScore(candidate, request) >= 16m)
        {
            reasons.Add("Good regional alignment");
        }

        if (AudienceScore(candidate, request) >= 15m)
        {
            reasons.Add("Audience profile overlap");
        }
        else if (AudienceScore(candidate, request) >= 10m)
        {
            reasons.Add("Language or audience fit");
        }

        if (MediaPreferenceScore(candidate, request) >= 15m)
        {
            reasons.Add("Matches requested channel mix");
        }

        if (BudgetScore(candidate, request) >= 12m)
        {
            reasons.Add("Fits comfortably within budget");
        }

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            if (IsPackageTotalCandidate(candidate))
            {
                reasons.Add("Fixed supplier package investment");
            }
            else if (IsPerSpotRateCardCandidate(candidate))
            {
                reasons.Add("Per-spot rate card pricing");
            }

            if (!string.IsNullOrWhiteSpace(candidate.TimeBand)
                && (Matches(candidate.TimeBand, "breakfast") || Matches(candidate.TimeBand, "drive")))
            {
                reasons.Add("High-impact radio daypart");
            }

            if (IsNationalCapableRadioCandidate(candidate, request) && request.SelectedBudget >= _policyOptions.Scale.BudgetFloor)
            {
                reasons.Add("Supports higher-band radio policy");
            }
        }

        if (candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("OOH prioritized for visibility");
            reasons.Add("Adds visible market presence");
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
    }

    private string[] BuildPolicyFlags(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var flags = new List<string>();

        if (request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor)
        {
            flags.Add("dominance_policy");
        }
        else if (request.SelectedBudget >= _policyOptions.Scale.BudgetFloor)
        {
            flags.Add("scale_policy");
        }

        if (request.PreferredMediaTypes.Count > 0)
        {
            flags.Add("preferred_media_applied");
        }

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) && IsNationalCapableRadioCandidate(candidate, request))
        {
            flags.Add("national_capable_radio");
        }

        flags.Add(GetPricingModel(candidate));

        if (candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            flags.Add("ooh_priority");
        }

        if (!string.IsNullOrWhiteSpace(candidate.RegionClusterCode))
        {
            flags.Add($"region:{candidate.RegionClusterCode.Trim().ToLowerInvariant()}");
        }

        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsRepeatableCandidate(InventoryCandidate candidate)
    {
        return !IsPackageTotalCandidate(candidate) && !IsFixedPlacementCandidate(candidate);
    }

    private static bool IsPackageTotalCandidate(InventoryCandidate candidate)
    {
        return GetPricingModel(candidate).Equals("package_total", StringComparison.OrdinalIgnoreCase)
            || candidate.PackageOnly;
    }

    private static bool IsPerSpotRateCardCandidate(InventoryCandidate candidate)
    {
        return GetPricingModel(candidate).Equals("per_spot_rate_card", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFixedPlacementCandidate(InventoryCandidate candidate)
    {
        return GetPricingModel(candidate).Equals("fixed_placement_total", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPricingModel(InventoryCandidate candidate)
    {
        if (candidate.Metadata.TryGetValue("pricingModel", out var value) && value is not null)
        {
            var normalized = value.ToString();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized.Trim();
            }
        }

        return candidate.SourceType switch
        {
            "radio_package" => "package_total",
            "radio_slot" => "per_spot_rate_card",
            "ooh" => "fixed_placement_total",
            _ => candidate.PackageOnly ? "package_total" : "unit_rate"
        };
    }

    private static decimal CalculateConfidenceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var score = 0.45m;

        if (GeoScore(candidate, request) >= 16m) score += 0.15m;
        if (AudienceScore(candidate, request) > 0m) score += 0.12m;
        if (MediaPreferenceScore(candidate, request) > 0m) score += 0.08m;
        if (!string.IsNullOrWhiteSpace(candidate.RegionClusterCode)) score += 0.05m;
        if (!string.IsNullOrWhiteSpace(candidate.Language)) score += 0.05m;
        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate.TimeBand)) score += 0.05m;
        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate.SlotType)) score += 0.03m;

        return Math.Min(0.95m, decimal.Round(score, 2));
    }

    private readonly record struct CandidateAnalysis(
        decimal Score,
        string[] SelectionReasons,
        string[] PolicyFlags,
        decimal ConfidenceScore);

    private readonly record struct PolicyOutcome(
        List<InventoryCandidate> Candidates,
        IReadOnlyList<string> FallbackFlags);
}

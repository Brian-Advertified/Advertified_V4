using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class MediaPlanningEngine : IMediaPlanningEngine
{
    private const decimal ScaleBudgetFloor = 150000m;
    private const decimal DominanceBudgetFloor = 500000m;
    private readonly IPlanningInventoryRepository _repository;

    public MediaPlanningEngine(IPlanningInventoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<RecommendationResult> GenerateAsync(
        CampaignPlanningRequest request,
        CancellationToken cancellationToken)
    {
        var ooh = await _repository.GetOohCandidatesAsync(request, cancellationToken);
        var radioSlots = await _repository.GetRadioSlotCandidatesAsync(request, cancellationToken);
        var radioPackages = await _repository.GetRadioPackageCandidatesAsync(request, cancellationToken);

        var allCandidates = ooh
            .Concat(radioSlots)
            .Concat(radioPackages)
            .Where(x => x.IsAvailable)
            .Where(x => !request.ExcludedMediaTypes.Contains(x.MediaType, StringComparer.OrdinalIgnoreCase))
            .ToList();

        allCandidates = ApplyHigherBandRadioPolicy(allCandidates, request);

        foreach (var candidate in allCandidates)
        {
            candidate.Score = ScoreCandidate(candidate, request);
        }

        var scored = allCandidates
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

        return new RecommendationResult
        {
            BasePlan = basePlan,
            RecommendedPlan = recommendedPlan,
            Upsells = upsells,
            Rationale = BuildRationale(basePlan, recommendedPlan, request)
        };
    }

    private static decimal ScoreCandidate(InventoryCandidate c, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        score += GeoScore(c, request);
        score += AudienceScore(c, request);
        score += BudgetScore(c, request);
        score += MediaPreferenceScore(c, request);
        score += AvailabilityScore(c);

        if (c.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            score += RadioFitBonus(c, request);
        }

        return score;
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

    private static decimal RadioFitBonus(InventoryCandidate c, CampaignPlanningRequest request)
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

    private static List<InventoryCandidate> ApplyHigherBandRadioPolicy(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        if (request.SelectedBudget < ScaleBudgetFloor)
        {
            return candidates;
        }

        var radioCandidates = candidates
            .Where(candidate => candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (radioCandidates.Count == 0)
        {
            return candidates;
        }

        var nationalRadioCandidates = radioCandidates
            .Where(candidate => IsNationalCapableRadioCandidate(candidate, request))
            .ToList();

        var minimumNationalCandidates = request.SelectedBudget >= DominanceBudgetFloor ? 2 : 1;
        if (nationalRadioCandidates.Count < minimumNationalCandidates)
        {
            return candidates;
        }

        return candidates
            .Where(candidate => !candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) || IsNationalCapableRadioCandidate(candidate, request))
            .ToList();
    }

    private static decimal GetHigherBandRadioBonus(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) || request.SelectedBudget < ScaleBudgetFloor)
        {
            return 0m;
        }

        var isNational = IsNationalCapableRadioCandidate(candidate, request);
        var isRegionalOnly = IsRegionalOrProvincialRadioCandidate(candidate);

        if (request.SelectedBudget >= DominanceBudgetFloor)
        {
            if (isNational)
            {
                return 18m;
            }

            return isRegionalOnly ? -24m : -12m;
        }

        if (isNational)
        {
            return 12m;
        }

        return isRegionalOnly ? -16m : -8m;
    }

    private static bool IsNationalCapableRadioCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
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

        if (request.SelectedBudget >= DominanceBudgetFloor)
        {
            return isNational && isFlagshipOrPremium;
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
}

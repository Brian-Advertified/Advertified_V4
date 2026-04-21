using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class RecommendationPlanBuilder : IRecommendationPlanBuilder
{
    private readonly IPlanningPolicyService _policyService;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;
    private static readonly string[] BroadcastGeoKeys = { "cityLabels", "city_labels", "city", "area" };

    public RecommendationPlanBuilder(IPlanningPolicyService policyService, IBroadcastMasterDataService broadcastMasterDataService)
    {
        _policyService = policyService;
        _broadcastMasterDataService = broadcastMasterDataService;
    }

    public List<PlannedItem> BuildPlan(List<InventoryCandidate> candidates, CampaignPlanningRequest request, bool diversify)
    {
        if (request.BudgetAllocation?.CompositeAllocations.Count > 0)
        {
            return BuildPlanWithBudgetAllocation(candidates, request);
        }

        if (HasTargetMix(request))
        {
            return BuildPlanWithTargetMix(candidates, request);
        }

        var result = new List<PlannedItem>();
        var spent = 0m;
        var usedMediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedSourceIds = new HashSet<Guid>();

        while (true)
        {
            var candidate = OrderCandidatesForSelection(candidates, result, request)
                .FirstOrDefault(item =>
                    !usedSourceIds.Contains(item.SourceId)
                    && item.Cost > 0m
                    && spent + item.Cost <= request.SelectedBudget
                    && !ExceedsStationDiversityCap(result, item)
                    && (!request.MaxMediaItems.HasValue || result.Count < request.MaxMediaItems.Value)
                    && (!diversify
                        || !usedMediaTypes.Contains(item.MediaType)
                        || usedMediaTypes.Count >= 2));

            if (candidate is null)
            {
                break;
            }

            if (candidate.Cost <= 0) continue;

            result.Add(ToPlannedItem(candidate));
            spent += candidate.Cost;
            usedMediaTypes.Add(candidate.MediaType);
            usedSourceIds.Add(candidate.SourceId);
        }

        FillBudgetGap(result, candidates, request, request.SelectedBudget, request.MaxMediaItems);

        if (result.Count == 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Cost <= request.SelectedBudget)
                {
                    result.Add(ToPlannedItem(candidate));
                    break;
                }
            }
        }

        return result;
    }

    private List<PlannedItem> BuildPlanWithBudgetAllocation(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        var allocation = request.BudgetAllocation;
        if (allocation is null || allocation.CompositeAllocations.Count == 0)
        {
            return HasTargetMix(request)
                ? BuildPlanWithTargetMix(candidates, request)
                : new List<PlannedItem>();
        }

        var result = new List<PlannedItem>();
        var usedSourceIds = new HashSet<Guid>();
        var spentTotal = 0m;
        var channelSpendTargets = BuildChannelSpendTargets(request);
        var allocationTargets = allocation.CompositeAllocations
            .Where(entry => entry.Amount > 0m)
            .OrderByDescending(entry => entry.Amount)
            .ToList();

        foreach (var target in allocationTargets)
        {
            if (request.MaxMediaItems.HasValue && result.Count >= request.MaxMediaItems.Value)
            {
                break;
            }

            if (result.Any(item => MatchesChannel(item.MediaType, target.Channel)))
            {
                continue;
            }

            var seedCandidate = SelectAllocationCandidate(candidates, result, request, target, usedSourceIds, spentTotal, channelSpendTargets, requireBucketMatch: true)
                ?? SelectAllocationCandidate(candidates, result, request, target, usedSourceIds, spentTotal, channelSpendTargets, requireBucketMatch: false);
            if (seedCandidate is null)
            {
                continue;
            }

            AddAllocatedItem(result, seedCandidate, target.Bucket);
            usedSourceIds.Add(seedCandidate.SourceId);
            spentTotal += seedCandidate.Cost;
        }

        var progressed = true;
        while (progressed)
        {
            progressed = false;

            foreach (var target in allocationTargets)
            {
                if (request.MaxMediaItems.HasValue && result.Count >= request.MaxMediaItems.Value)
                {
                    break;
                }

                var spentForTarget = result
                    .Where(item => MatchesChannel(item.MediaType, target.Channel))
                    .Where(item => string.Equals(GetMetadataString(item.Metadata, "allocationGeoBucket"), target.Bucket, StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.TotalCost);
                if (spentForTarget >= target.Amount)
                {
                    continue;
                }

                var nextCandidate = SelectAllocationCandidate(candidates, result, request, target, usedSourceIds, spentTotal, channelSpendTargets, requireBucketMatch: true)
                    ?? SelectAllocationCandidate(candidates, result, request, target, usedSourceIds, spentTotal, channelSpendTargets, requireBucketMatch: false);
                if (nextCandidate is null)
                {
                    continue;
                }

                AddAllocatedItem(result, nextCandidate, ResolveGeoBucket(nextCandidate, request, target.RadiusKm));
                usedSourceIds.Add(nextCandidate.SourceId);
                spentTotal += nextCandidate.Cost;
                progressed = true;
            }
        }

        FillBudgetGap(result, candidates, request, request.SelectedBudget, request.MaxMediaItems, channelSpendTargets);

        if (result.Count == 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Cost <= request.SelectedBudget)
                {
                    AddAllocatedItem(result, candidate, ResolveGeoBucket(candidate, request, nearbyRadiusKm: null));
                    break;
                }
            }
        }

        return result;
    }

    private List<PlannedItem> BuildPlanWithTargetMix(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        var result = new List<PlannedItem>();
        var usedSourceIds = new HashSet<Guid>();
        var spentTotal = 0m;
        var requestedShares = _policyService.GetRequestedChannelShares(request)
            .Select(share => (share.Channel, share.Share))
            .ToList();
        var channelSpendTargets = requestedShares.ToDictionary(
            entry => entry.Channel,
            entry => decimal.Round(request.SelectedBudget * entry.Share / 100m, 2, MidpointRounding.AwayFromZero),
            StringComparer.OrdinalIgnoreCase);

        // First ensure at least one item for each requested channel when inventory allows.
        foreach (var shareTarget in requestedShares.OrderByDescending(entry => entry.Share))
        {
            if (request.MaxMediaItems.HasValue && result.Count >= request.MaxMediaItems.Value)
            {
                break;
            }

                var channelCandidates = candidates
                    .Where(candidate => MatchesChannel(candidate.MediaType, shareTarget.Channel))
                    .Where(candidate => candidate.Cost > 0m)
                    .Where(candidate => !usedSourceIds.Contains(candidate.SourceId))
                    .Where(candidate => spentTotal + candidate.Cost <= request.SelectedBudget)
                    .Where(candidate => !ExceedsStationDiversityCap(result, candidate))
                    .Where(candidate => CanSpendInChannel(result, candidate, channelSpendTargets));

            // If the user selected a suburb, try to anchor radio/TV with a station whose
            // broadcast geo labels include that suburb token (e.g. "Soweto"). If none match,
            // fall back to normal scoring so we still return a plan.
            if (shareTarget.Channel.Equals("radio", StringComparison.OrdinalIgnoreCase)
                && request.Suburbs.Count > 0)
            {
                var suburbTokens = ExtractSuburbTokens(request.Suburbs);
                if (suburbTokens.Length > 0)
                {
                    var matched = channelCandidates
                        .Where(candidate => MatchesAnyBroadcastGeoToken(candidate, suburbTokens))
                        .OrderByDescending(candidate => ScoreRequestedLanguageCoverage(candidate, result, request))
                        .ThenByDescending(candidate => HasMatchingOohSite(result, candidate))
                        .ThenByDescending(candidate => FitsChannelTarget(result, candidate, channelSpendTargets))
                        .ThenBy(candidate => ChannelOvershootAmount(result, candidate, channelSpendTargets))
                        .ThenBy(candidate => GetSelectionPriority(candidate))
                        .ThenBy(candidate => GetStationSelectionCount(result, candidate, shareTarget.Channel))
                        .ThenByDescending(candidate => candidate.Score)
                        .ThenByDescending(candidate => candidate.Cost)
                        .FirstOrDefault();

                    if (matched is not null)
                    {
                        result.Add(ToPlannedItem(matched));
                        usedSourceIds.Add(matched.SourceId);
                        spentTotal += matched.Cost;
                        continue;
                    }
                }
            }

            var channelCandidate = channelCandidates
                .OrderByDescending(candidate => ScoreRequestedLanguageCoverage(candidate, result, request))
                .ThenByDescending(candidate => HasMatchingOohSite(result, candidate))
                .ThenByDescending(candidate => FitsChannelTarget(result, candidate, channelSpendTargets))
                .ThenBy(candidate => ChannelOvershootAmount(result, candidate, channelSpendTargets))
                .ThenByDescending(candidate => GetOohValuePreference(candidate, request))
                .ThenBy(candidate => GetOohFormatSelectionCount(result, candidate))
                .ThenBy(candidate => GetSelectionPriority(candidate))
                .ThenBy(candidate => GetStationSelectionCount(result, candidate, shareTarget.Channel))
                .ThenByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Cost)
                .FirstOrDefault();

            if (channelCandidate is null)
            {
                continue;
            }

            result.Add(ToPlannedItem(channelCandidate));
            usedSourceIds.Add(channelCandidate.SourceId);
            spentTotal += channelCandidate.Cost;
        }

        // Then keep filling each requested channel toward its budget share target.
        var progressed = true;
        while (progressed)
        {
            progressed = false;

            foreach (var shareTarget in requestedShares.OrderByDescending(entry => entry.Share))
            {
                if (request.MaxMediaItems.HasValue && result.Count >= request.MaxMediaItems.Value)
                {
                    break;
                }

                var alreadySpentForChannel = result
                    .Where(item => MatchesChannel(item.MediaType, shareTarget.Channel))
                    .Sum(item => item.TotalCost);
                if (alreadySpentForChannel >= channelSpendTargets[shareTarget.Channel])
                {
                    continue;
                }

                var nextCandidate = candidates
                    .Where(candidate => MatchesChannel(candidate.MediaType, shareTarget.Channel))
                    .Where(candidate => candidate.Cost > 0m)
                    .Where(candidate => !usedSourceIds.Contains(candidate.SourceId))
                    .Where(candidate => spentTotal + candidate.Cost <= request.SelectedBudget)
                    .Where(candidate => !ExceedsStationDiversityCap(result, candidate))
                    .Where(candidate => CanSpendInChannel(result, candidate, channelSpendTargets))
                    .OrderByDescending(candidate => ScoreRequestedLanguageCoverage(candidate, result, request))
                    .ThenByDescending(candidate => HasMatchingOohSite(result, candidate))
                    .ThenByDescending(candidate => FitsChannelTarget(result, candidate, channelSpendTargets))
                    .ThenBy(candidate => ChannelOvershootAmount(result, candidate, channelSpendTargets))
                    .ThenByDescending(candidate => GetOohValuePreference(candidate, request))
                    .ThenBy(candidate => GetOohFormatSelectionCount(result, candidate))
                    .ThenBy(candidate => GetSelectionPriority(candidate))
                    .ThenBy(candidate => GetStationSelectionCount(result, candidate, shareTarget.Channel))
                    .ThenByDescending(candidate => candidate.Score)
                    .ThenByDescending(candidate => candidate.Cost)
                    .FirstOrDefault();

                if (nextCandidate is null)
                {
                    continue;
                }

                result.Add(ToPlannedItem(nextCandidate));
                usedSourceIds.Add(nextCandidate.SourceId);
                spentTotal += nextCandidate.Cost;
                progressed = true;
            }
        }

        FillBudgetGap(result, candidates, request, request.SelectedBudget, request.MaxMediaItems, channelSpendTargets);

        if (result.Count == 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Cost <= request.SelectedBudget)
                {
                    result.Add(ToPlannedItem(candidate));
                    break;
                }
            }
        }

        return result;
    }

    public List<PlannedItem> BuildUpsells(List<InventoryCandidate> candidates, List<PlannedItem> recommendedPlan, decimal upsellHeadroom)
    {
        var selectedIds = recommendedPlan.Select(x => x.SourceId).ToHashSet();
        var result = new List<PlannedItem>();
        var spent = 0m;

        foreach (var candidate in candidates
                     .Where(x => !selectedIds.Contains(x.SourceId))
                     .OrderBy(candidate => GetSelectionPriority(candidate))
                     .ThenByDescending(candidate => candidate.Score)
                     .ThenByDescending(candidate => candidate.Cost))
        {
            if (candidate.Cost <= 0) continue;
            if (spent + candidate.Cost > upsellHeadroom) continue;

            result.Add(ToPlannedItem(candidate));
            spent += candidate.Cost;

            if (result.Count >= 3) break;
        }

        return result;
    }

    private void FillBudgetGap(
        List<PlannedItem> result,
        List<InventoryCandidate> candidates,
        CampaignPlanningRequest request,
        decimal budget,
        int? maxItems,
        IReadOnlyDictionary<string, decimal>? channelSpendTargets = null)
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
            .ToList();

        if (fillCandidates.Count == 0)
        {
            return;
        }

        // Exact-fill backtracking can become expensive when candidate pools are large.
        // Guard it to keep recommendation generation responsive in production traffic.
        var shouldAttemptExactFill = fillCandidates.Count <= 40;
        var maxDepth = shouldAttemptExactFill
            ? Math.Min(4, Math.Max(2, maxItems.GetValueOrDefault(result.Count + 2)))
            : 0;
        var exactFill = shouldAttemptExactFill
            ? TryBuildExactFill(fillCandidates, remaining, maxDepth, maxItems, result, channelSpendTargets)
            : Array.Empty<InventoryCandidate>();

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
                    !ExceedsStationDiversityCap(result, x) &&
                    CanSpendInChannel(result, x, channelSpendTargets) &&
                    ((result.Any(item => item.SourceId == x.SourceId) && _policyService.IsRepeatableCandidate(x))
                        || !maxItems.HasValue
                        || result.Count < maxItems.Value))
                .OrderByDescending(x => ScoreRequestedLanguageCoverage(x, result, request))
                .ThenByDescending(x => HasMatchingOohSite(result, x))
                .ThenByDescending(x => FitsChannelTarget(result, x, channelSpendTargets))
                .ThenBy(x => ChannelOvershootAmount(result, x, channelSpendTargets))
                .ThenByDescending(x => GetOohValuePreference(x, request))
                .ThenBy(x => GetOohFormatSelectionCount(result, x))
                .ThenBy(x => GetSelectionPriority(x))
                .ThenBy(x => GetStationSelectionCount(result, x, x.MediaType))
                .ThenByDescending(x => x.Score)
                .ThenByDescending(x => x.Cost)
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

    private IReadOnlyList<InventoryCandidate> TryBuildExactFill(
        IReadOnlyList<InventoryCandidate> candidates,
        decimal remaining,
        int depthRemaining,
        int? maxItems,
        IReadOnlyList<PlannedItem> currentPlan,
        IReadOnlyDictionary<string, decimal>? channelSpendTargets)
    {
        if (remaining == 0m)
        {
            return Array.Empty<InventoryCandidate>();
        }

        if (depthRemaining <= 0)
        {
            return Array.Empty<InventoryCandidate>();
        }

        foreach (var candidate in candidates
                     .Where(x => x.Cost <= remaining)
                     .Where(x => CanSpendInChannel(currentPlan, x, channelSpendTargets))
                     .OrderBy(x => GetSelectionPriority(x))
                     .ThenByDescending(x => FitsChannelTarget(currentPlan, x, channelSpendTargets))
                     .ThenBy(x => ChannelOvershootAmount(currentPlan, x, channelSpendTargets))
                     .ThenByDescending(x => x.Score)
                     .ThenByDescending(x => x.Cost))
        {
            var wouldAddNewLine = currentPlan.All(item => item.SourceId != candidate.SourceId);
            var wouldRepeatExisting = !wouldAddNewLine;

            if (wouldRepeatExisting && !_policyService.IsRepeatableCandidate(candidate))
            {
                continue;
            }

            if (ExceedsStationDiversityCap(currentPlan, candidate))
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
            var tail = TryBuildExactFill(candidates, remaining - candidate.Cost, depthRemaining - 1, maxItems, simulatedPlan, channelSpendTargets);
            if (tail.Count > 0 || remaining - candidate.Cost == 0m)
            {
                return new[] { candidate }.Concat(tail).ToArray();
            }
        }

        return Array.Empty<InventoryCandidate>();
    }

    private static PlannedItem ToPlannedItem(InventoryCandidate candidate, string? allocationGeoBucket = null)
    {
        var metadata = new Dictionary<string, object?>(candidate.Metadata);
        SetMetadataIfMissing(metadata, "province", candidate.Province);
        SetMetadataIfMissing(metadata, "city", candidate.City);
        SetMetadataIfMissing(metadata, "suburb", candidate.Suburb);
        SetMetadataIfMissing(metadata, "area", candidate.Area);
        if (!string.IsNullOrWhiteSpace(allocationGeoBucket))
        {
            metadata["allocationGeoBucket"] = allocationGeoBucket.Trim().ToLowerInvariant();
        }

        return new PlannedItem
        {
            SourceId = candidate.SourceId,
            SourceType = candidate.SourceType,
            DisplayName = candidate.DisplayName,
            MediaType = candidate.MediaType,
            UnitCost = candidate.Cost,
            Quantity = 1,
            Score = candidate.Score,
            Metadata = metadata
        };
    }

    private void AddOrIncrement(List<PlannedItem> result, InventoryCandidate candidate)
    {
        var existing = result.FirstOrDefault(item => item.SourceId == candidate.SourceId);
        if (existing is not null)
        {
            if (!_policyService.IsRepeatableCandidate(candidate))
            {
                return;
            }

            if (ExceedsStationDiversityCap(result, candidate))
            {
                return;
            }

            existing.Quantity += 1;
            return;
        }

        result.Add(ToPlannedItem(candidate));
    }

    private static void AddAllocatedItem(List<PlannedItem> result, InventoryCandidate candidate, string geoBucket)
    {
        result.Add(ToPlannedItem(candidate, geoBucket));
    }

    private IEnumerable<InventoryCandidate> OrderCandidatesForSelection(
        IEnumerable<InventoryCandidate> candidates,
        IReadOnlyList<PlannedItem> currentPlan,
        CampaignPlanningRequest request)
    {
        return candidates
            .OrderByDescending(candidate => ScoreRequestedLanguageCoverage(candidate, currentPlan, request))
            .ThenByDescending(candidate => HasMatchingOohSite(currentPlan, candidate))
            .ThenByDescending(candidate => GetOohValuePreference(candidate, request))
            .ThenBy(candidate => GetOohFormatSelectionCount(currentPlan, candidate))
            .ThenBy(candidate => GetSelectionPriority(candidate))
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Cost);
    }

    private static int GetSelectionPriority(InventoryCandidate candidate)
    {
        return PlanningChannelSupport.NormalizeChannel(candidate.MediaType) switch
        {
            PlanningChannelSupport.Billboard => 0,
            PlanningChannelSupport.DigitalScreen => 0,
            _ => 0
        };
    }

    private static decimal GetOohValuePreference(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!PlanningChannelSupport.IsOohFamilyChannel(candidate.MediaType))
        {
            return 0m;
        }

        decimal score = 0m;
        var budget = Math.Max(1m, request.SelectedBudget);
        var costRatio = candidate.Cost / budget;

        score += costRatio switch
        {
            >= 0.18m => 8m,
            >= 0.12m => 6m,
            >= 0.08m => 4m,
            >= 0.05m => 2m,
            _ => 0m
        };

        if (MatchesMetadataToken(candidate, "premium_mall", "venueType", "venue_type"))
        {
            score += 5m;
        }
        else if (MatchesMetadataToken(candidate, "lifestyle_centre", "venueType", "venue_type"))
        {
            score += 3m;
        }
        else if (MatchesMetadataToken(candidate, "community_mall", "venueType", "venue_type"))
        {
            score += 2m;
        }

        if (MatchesMetadataToken(candidate, "mall_interior", "environmentType", "environment_type")
            || MatchesMetadataToken(candidate, "food_court", "environmentType", "environment_type"))
        {
            score += 4m;
        }

        score += ScoreFitBand(candidate, "highValueShopperFit", "high_value_shopper_fit", high: 5m, medium: 2m);
        score += ScoreFitBand(candidate, "dwellTimeScore", "dwell_time_score", high: 4m, medium: 2m);

        if (PlanningChannelSupport.NormalizeChannel(candidate.MediaType) == PlanningChannelSupport.DigitalScreen)
        {
            score += 2m;
        }
        else if (MatchesMetadataToken(candidate, "roadside", "environmentType", "environment_type")
            || MatchesMetadataToken(candidate, "outdoor", "environmentType", "environment_type"))
        {
            score -= costRatio < 0.05m ? 4m : 1m;
        }

        return score;
    }

    private static int GetOohFormatSelectionCount(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate)
    {
        if (!PlanningChannelSupport.IsOohFamilyChannel(candidate.MediaType))
        {
            return 0;
        }

        var candidateFormat = PlanningChannelSupport.NormalizeChannel(candidate.MediaType);
        return currentPlan.Count(item => PlanningChannelSupport.NormalizeChannel(item.MediaType) == candidateFormat);
    }

    private static IReadOnlyDictionary<string, decimal>? BuildChannelSpendTargets(CampaignPlanningRequest request)
    {
        if (request.BudgetAllocation?.ChannelAllocations.Count > 0)
        {
            return request.BudgetAllocation.ChannelAllocations
                .Where(entry => entry.Amount > 0m)
                .GroupBy(entry => GetSpendTargetChannelKey(entry.Channel), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(entry => entry.Amount),
                    StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    private static bool CanSpendInChannel(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate,
        IReadOnlyDictionary<string, decimal>? channelSpendTargets)
    {
        if (channelSpendTargets is null || channelSpendTargets.Count == 0)
        {
            return true;
        }

        var channelKey = GetSpendTargetChannelKey(candidate.MediaType);
        if (!channelSpendTargets.TryGetValue(channelKey, out var targetAmount) || targetAmount <= 0m)
        {
            return true;
        }

        var currentSpend = GetChannelSpend(currentPlan, channelKey);
        var maxAllowed = targetAmount + GetChannelOvershootTolerance(targetAmount);
        if (currentSpend + candidate.Cost <= maxAllowed)
        {
            return true;
        }

        return currentSpend <= 0m && candidate.Cost <= maxAllowed;
    }

    private static bool FitsChannelTarget(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate,
        IReadOnlyDictionary<string, decimal>? channelSpendTargets)
    {
        if (channelSpendTargets is null || channelSpendTargets.Count == 0)
        {
            return true;
        }

        var channelKey = GetSpendTargetChannelKey(candidate.MediaType);
        if (!channelSpendTargets.TryGetValue(channelKey, out var targetAmount) || targetAmount <= 0m)
        {
            return true;
        }

        var currentSpend = GetChannelSpend(currentPlan, channelKey);
        return currentSpend + candidate.Cost <= targetAmount;
    }

    private static decimal ChannelOvershootAmount(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate,
        IReadOnlyDictionary<string, decimal>? channelSpendTargets)
    {
        if (channelSpendTargets is null || channelSpendTargets.Count == 0)
        {
            return 0m;
        }

        var channelKey = GetSpendTargetChannelKey(candidate.MediaType);
        if (!channelSpendTargets.TryGetValue(channelKey, out var targetAmount) || targetAmount <= 0m)
        {
            return 0m;
        }

        var currentSpend = GetChannelSpend(currentPlan, channelKey);
        return Math.Max(0m, currentSpend + candidate.Cost - targetAmount);
    }

    private static decimal GetChannelSpend(IReadOnlyList<PlannedItem> currentPlan, string channelKey)
    {
        return currentPlan
            .Where(item => string.Equals(GetSpendTargetChannelKey(item.MediaType), channelKey, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.TotalCost);
    }

    private static string GetSpendTargetChannelKey(string? mediaType)
    {
        var normalized = PlanningChannelSupport.NormalizeChannel(mediaType);
        return PlanningChannelSupport.IsOohFamilyChannel(normalized)
            ? PlanningChannelSupport.OohAlias
            : normalized;
    }

    private static decimal GetChannelOvershootTolerance(decimal targetAmount)
    {
        return Math.Max(5000m, decimal.Round(targetAmount * 0.10m, 2, MidpointRounding.AwayFromZero));
    }

    private decimal ScoreRequestedLanguageCoverage(
        InventoryCandidate candidate,
        IReadOnlyList<PlannedItem> currentPlan,
        CampaignPlanningRequest request)
    {
        if (request.TargetLanguages.Count == 0
            || !(candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
                || candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase)))
        {
            return 0m;
        }

        var requested = BroadcastLanguageSupport.NormalizeRequestedLanguages(request.TargetLanguages, _broadcastMasterDataService.NormalizeLanguageCode);
        if (requested.Count == 0)
        {
            return 0m;
        }

        var candidateLanguages = BroadcastLanguageSupport.ExtractCandidateLanguageCodes(candidate, _broadcastMasterDataService.NormalizeLanguageCode);
        if (candidateLanguages.Count == 0)
        {
            return 0m;
        }

        var selectedCoverage = currentPlan
            .Where(item => MatchesChannel(item.MediaType, candidate.MediaType))
            .SelectMany(item => BroadcastLanguageSupport.ExtractPlannedItemLanguageCodes(item, _broadcastMasterDataService.NormalizeLanguageCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        decimal score = 0m;
        for (var index = 0; index < requested.Count; index++)
        {
            var language = requested[index];
            if (!candidateLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var baseWeight = index switch
            {
                0 => 80m,
                1 => 52m,
                2 => 36m,
                3 => 24m,
                _ => Math.Max(8m, 18m - index)
            };

            score += selectedCoverage.Contains(language) ? baseWeight * 0.15m : baseWeight;
        }

        var coversAllRequested = requested.All(language => candidateLanguages.Contains(language, StringComparer.OrdinalIgnoreCase));
        if (coversAllRequested)
        {
            score += 120m;
        }

        var addsMissingLanguages = requested
            .Count(language => !selectedCoverage.Contains(language) && candidateLanguages.Contains(language, StringComparer.OrdinalIgnoreCase));
        if (addsMissingLanguages > 1)
        {
            score += 18m * (addsMissingLanguages - 1);
        }

        return score;
    }

    private static bool HasTargetMix(CampaignPlanningRequest request)
    {
        return request.TargetRadioShare.GetValueOrDefault() > 0
            || request.TargetOohShare.GetValueOrDefault() > 0
            || request.TargetTvShare.GetValueOrDefault() > 0
            || request.TargetDigitalShare.GetValueOrDefault() > 0
            || request.BudgetAllocation?.ChannelAllocations.Count > 0;
    }

    private static string[] ExtractSuburbTokens(IEnumerable<string> suburbs)
    {
        return suburbs
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool MatchesAnyBroadcastGeoToken(InventoryCandidate candidate, IReadOnlyList<string> tokens)
    {
        if (candidate.Metadata.Count == 0 || tokens.Count == 0)
        {
            return false;
        }

        foreach (var key in BroadcastGeoKeys)
        {
            if (!candidate.Metadata.TryGetValue(key, out var raw))
            {
                continue;
            }

            var values = ExtractMetadataTokens(raw);
            if (values.Length == 0)
            {
                continue;
            }

            if (tokens.Any(token => values.Any(value => string.Equals(token, value, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] ExtractMetadataTokens(object? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : new[] { text.Trim() };
        }

        if (value is IEnumerable<string> textValues)
        {
            return textValues
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .Select(static entry => entry.Trim())
                .ToArray();
        }

        if (value is System.Text.Json.JsonElement json)
        {
            if (json.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var jsonText = json.GetString();
                return string.IsNullOrWhiteSpace(jsonText) ? Array.Empty<string>() : new[] { jsonText.Trim() };
            }

            if (json.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return json
                    .EnumerateArray()
                    .Where(static item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(static item => item.GetString())
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Select(static item => item!.Trim())
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        var fallback = value.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? Array.Empty<string>() : new[] { fallback.Trim() };
    }

    private static bool MatchesChannel(string? mediaType, string requestedChannel)
    {
        return PlanningChannelSupport.MatchesRequestedChannel(mediaType, requestedChannel);
    }

    private InventoryCandidate? SelectAllocationCandidate(
        IReadOnlyList<InventoryCandidate> candidates,
        IReadOnlyList<PlannedItem> currentPlan,
        CampaignPlanningRequest request,
        PlanningAllocationLine target,
        ISet<Guid> usedSourceIds,
        decimal spentTotal,
        IReadOnlyDictionary<string, decimal>? channelSpendTargets,
        bool requireBucketMatch)
    {
        return candidates
            .Where(candidate => MatchesChannel(candidate.MediaType, target.Channel))
            .Where(candidate => candidate.Cost > 0m)
            .Where(candidate => !usedSourceIds.Contains(candidate.SourceId))
            .Where(candidate => spentTotal + candidate.Cost <= request.SelectedBudget)
            .Where(candidate => !ExceedsStationDiversityCap(currentPlan, candidate))
            .Where(candidate => CanSpendInChannel(currentPlan, candidate, channelSpendTargets))
            .Select(candidate => new
            {
                Candidate = candidate,
                GeoBucket = ResolveGeoBucket(candidate, request, target.RadiusKm)
            })
            .Where(entry => !requireBucketMatch || string.Equals(entry.GeoBucket, target.Bucket, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => string.Equals(entry.GeoBucket, target.Bucket, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(entry => ScoreRequestedLanguageCoverage(entry.Candidate, currentPlan, request))
            .ThenByDescending(entry => HasMatchingOohSite(currentPlan, entry.Candidate))
            .ThenByDescending(entry => FitsChannelTarget(currentPlan, entry.Candidate, channelSpendTargets))
            .ThenBy(entry => ChannelOvershootAmount(currentPlan, entry.Candidate, channelSpendTargets))
            .ThenBy(entry => GetSelectionPriority(entry.Candidate))
            .ThenBy(entry => GetStationSelectionCount(currentPlan, entry.Candidate, target.Channel))
            .ThenByDescending(entry => entry.Candidate.Score)
            .ThenByDescending(entry => entry.Candidate.Cost)
            .Select(entry => entry.Candidate)
            .FirstOrDefault();
    }

    private static string ResolveGeoBucket(InventoryCandidate candidate, CampaignPlanningRequest request, double? nearbyRadiusKm)
    {
        if (MatchesOriginBucket(candidate, request))
        {
            return "origin";
        }

        if (MatchesNearbyBucket(candidate, request, nearbyRadiusKm))
        {
            return "nearby";
        }

        return "wider";
    }

    private static bool MatchesOriginBucket(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var priorityAreas = request.Targeting?.PriorityAreas ?? request.MustHaveAreas;
        if (priorityAreas.Any(area => MatchesGeo(area, candidate.Area) || MatchesGeo(area, candidate.Suburb)))
        {
            return true;
        }

        var area = request.Targeting?.Areas.FirstOrDefault()
            ?? request.Targeting?.Label
            ?? request.BusinessLocation?.Area;
        var city = request.Targeting?.City
            ?? request.Targeting?.Cities.FirstOrDefault()
            ?? request.BusinessLocation?.City;
        var province = request.Targeting?.Province
            ?? request.Targeting?.Provinces.FirstOrDefault()
            ?? request.BusinessLocation?.Province;

        return MatchesGeo(area, candidate.Area)
            || MatchesGeo(area, candidate.Suburb)
            || MatchesGeo(city, candidate.City)
            || MatchesGeo(province, candidate.Province);
    }

    private static bool MatchesNearbyBucket(InventoryCandidate candidate, CampaignPlanningRequest request, double? nearbyRadiusKm)
    {
        var latitude = request.Targeting?.Latitude
            ?? request.TargetLatitude
            ?? request.BusinessLocation?.Latitude;
        var longitude = request.Targeting?.Longitude
            ?? request.TargetLongitude
            ?? request.BusinessLocation?.Longitude;
        var city = request.Targeting?.City
            ?? request.Targeting?.Cities.FirstOrDefault()
            ?? request.BusinessLocation?.City;

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return !string.IsNullOrWhiteSpace(city)
                && MatchesGeo(city, candidate.City)
                && !MatchesOriginBucket(candidate, request);
        }

        if (candidate.Latitude.HasValue
            && candidate.Longitude.HasValue
            && nearbyRadiusKm.HasValue)
        {
            var distanceKm = HaversineDistanceKm(
                latitude.Value,
                longitude.Value,
                candidate.Latitude.Value,
                candidate.Longitude.Value);

            if (distanceKm <= nearbyRadiusKm.Value)
            {
                return true;
            }
        }

        return !string.IsNullOrWhiteSpace(city)
            && MatchesGeo(city, candidate.City)
            && !MatchesOriginBucket(candidate, request);
    }

    private static bool MatchesGeo(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static double HaversineDistanceKm(double startLatitude, double startLongitude, double endLatitude, double endLongitude)
    {
        const double earthRadiusKm = 6371d;
        var latitudeDeltaRadians = ToRadians(endLatitude - startLatitude);
        var longitudeDeltaRadians = ToRadians(endLongitude - startLongitude);

        var a = Math.Sin(latitudeDeltaRadians / 2d) * Math.Sin(latitudeDeltaRadians / 2d)
            + Math.Cos(ToRadians(startLatitude)) * Math.Cos(ToRadians(endLatitude))
            * Math.Sin(longitudeDeltaRadians / 2d) * Math.Sin(longitudeDeltaRadians / 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static int GetStationSelectionCount(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate,
        string requestedChannel)
    {
        if (!string.Equals(requestedChannel, "tv", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(requestedChannel, "radio", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var station = GetStationKey(candidate.DisplayName);
        if (string.IsNullOrWhiteSpace(station))
        {
            return 0;
        }

        return currentPlan.Count(item =>
            string.Equals(item.MediaType?.Trim(), "tv", StringComparison.OrdinalIgnoreCase)
            && string.Equals(GetStationKey(item.DisplayName), station, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ExceedsStationDiversityCap(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate)
    {
        if (!IsStationDiversityChannel(candidate.MediaType))
        {
            return false;
        }

        var station = GetStationKey(candidate.DisplayName);
        if (string.IsNullOrWhiteSpace(station))
        {
            return false;
        }

        var cap = GetStationCap(candidate.MediaType);
        var currentCount = currentPlan
            .Where(item => string.Equals(item.MediaType?.Trim(), candidate.MediaType?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(GetStationKey(item.DisplayName), station, StringComparison.OrdinalIgnoreCase))
            .Sum(item => Math.Max(1, item.Quantity));

        return currentCount >= cap;
    }

    private static bool IsStationDiversityChannel(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.Trim().Equals("radio", StringComparison.OrdinalIgnoreCase)
            || mediaType.Trim().Equals("tv", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetStationCap(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return int.MaxValue;
        }

        return mediaType.Trim().Equals("radio", StringComparison.OrdinalIgnoreCase) ? 2 : 2;
    }

    private static string GetStationKey(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        var separator = displayName.IndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return displayName.Trim();
        }

        return displayName[..separator].Trim();
    }

    private static bool HasMatchingOohSite(IReadOnlyList<PlannedItem> currentPlan, InventoryCandidate candidate)
    {
        if (!PlanningChannelSupport.IsOohFamilyChannel(candidate.MediaType))
        {
            return false;
        }

        var candidateSiteKey = GetOohSiteKey(candidate.DisplayName, candidate.Area, candidate.Suburb, candidate.City);
        if (string.IsNullOrWhiteSpace(candidateSiteKey))
        {
            return false;
        }

        return currentPlan
            .Where(item => PlanningChannelSupport.IsOohFamilyChannel(item.MediaType))
            .Select(item => GetOohSiteKey(
                item.DisplayName,
                GetMetadataString(item.Metadata, "area"),
                GetMetadataString(item.Metadata, "suburb"),
                GetMetadataString(item.Metadata, "city")))
            .Any(siteKey => !string.IsNullOrWhiteSpace(siteKey) && string.Equals(siteKey, candidateSiteKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetOohSiteKey(string? displayName, string? area, string? suburb, string? city)
    {
        var preferred = FirstNonEmpty(area, suburb, ExtractOohDisplaySite(displayName), city);
        if (string.IsNullOrWhiteSpace(preferred))
        {
            return string.Empty;
        }

        return preferred.Trim().ToLowerInvariant();
    }

    private static string? ExtractOohDisplaySite(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var value = displayName.Trim();
        var separator = value.IndexOf(" - ", StringComparison.Ordinal);
        if (separator > 0)
        {
            value = value[..separator];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetMetadataString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool MatchesMetadataToken(InventoryCandidate candidate, string expected, params string[] keys)
    {
        var expectedValue = expected.Trim();
        if (string.IsNullOrWhiteSpace(expectedValue))
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (!candidate.Metadata.TryGetValue(key, out var raw) || raw is null)
            {
                continue;
            }

            var value = raw.ToString();
            if (!string.IsNullOrWhiteSpace(value)
                && string.Equals(value.Trim(), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static decimal ScoreFitBand(InventoryCandidate candidate, string camelKey, string snakeKey, decimal high, decimal medium)
    {
        var value = FirstNonEmpty(GetMetadataString(candidate.Metadata, camelKey), GetMetadataString(candidate.Metadata, snakeKey));
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "high" => high,
            "medium" => medium,
            _ => 0m
        };
    }

    private static void SetMetadataIfMissing(IDictionary<string, object?> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || metadata.ContainsKey(key))
        {
            return;
        }

        metadata[key] = value;
    }
}

using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class RecommendationPlanBuilder : IRecommendationPlanBuilder
{
    private readonly IPlanningPolicyService _policyService;

    public RecommendationPlanBuilder(IPlanningPolicyService policyService)
    {
        _policyService = policyService;
    }

    public List<PlannedItem> BuildPlan(List<InventoryCandidate> candidates, decimal budget, int? maxItems, bool diversify)
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

    public List<PlannedItem> BuildUpsells(List<InventoryCandidate> candidates, List<PlannedItem> recommendedPlan, decimal upsellHeadroom)
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

    private void FillBudgetGap(List<PlannedItem> result, List<InventoryCandidate> candidates, decimal budget, int? maxItems)
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
                    ((result.Any(item => item.SourceId == x.SourceId) && _policyService.IsRepeatableCandidate(x))
                        || !maxItems.HasValue
                        || result.Count < maxItems.Value))
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

    private IReadOnlyList<InventoryCandidate> TryBuildExactFill(
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

            if (wouldRepeatExisting && !_policyService.IsRepeatableCandidate(candidate))
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

    private void AddOrIncrement(List<PlannedItem> result, InventoryCandidate candidate)
    {
        var existing = result.FirstOrDefault(item => item.SourceId == candidate.SourceId);
        if (existing is not null)
        {
            if (!_policyService.IsRepeatableCandidate(candidate))
            {
                return;
            }

            existing.Quantity += 1;
            return;
        }

        result.Add(ToPlannedItem(candidate));
    }
}


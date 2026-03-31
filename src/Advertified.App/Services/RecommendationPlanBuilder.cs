using Advertified.App.Contracts.Campaigns;
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

    public List<PlannedItem> BuildPlan(List<InventoryCandidate> candidates, CampaignPlanningRequest request, bool diversify)
    {
        if (HasTargetMix(request))
        {
            return BuildPlanWithTargetMix(candidates, request);
        }

        var result = new List<PlannedItem>();
        var spent = 0m;
        var usedMediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (candidate.Cost <= 0) continue;
            if (spent + candidate.Cost > request.SelectedBudget) continue;
            if (request.MaxMediaItems.HasValue && result.Count >= request.MaxMediaItems.Value) break;

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

        FillBudgetGap(result, candidates, request.SelectedBudget, request.MaxMediaItems);

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

    private List<PlannedItem> BuildPlanWithTargetMix(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        var result = new List<PlannedItem>();
        var usedSourceIds = new HashSet<Guid>();
        var spentTotal = 0m;
        var requestedShares = GetRequestedShares(request);

        // First ensure at least one item for each requested channel when inventory allows.
        foreach (var shareTarget in requestedShares.OrderByDescending(entry => entry.Share))
        {
            if (request.MaxMediaItems.HasValue && result.Count >= request.MaxMediaItems.Value)
            {
                break;
            }

            var channelCandidate = candidates
                .Where(candidate => MatchesChannel(candidate.MediaType, shareTarget.Channel))
                .Where(candidate => candidate.Cost > 0m)
                .Where(candidate => !usedSourceIds.Contains(candidate.SourceId))
                .Where(candidate => spentTotal + candidate.Cost <= request.SelectedBudget)
                .OrderBy(candidate => GetStationSelectionCount(result, candidate, shareTarget.Channel))
                .ThenByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Cost)
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
        var channelTargetSpend = requestedShares.ToDictionary(
            entry => entry.Channel,
            entry => decimal.Round(request.SelectedBudget * entry.Share / 100m, 2),
            StringComparer.OrdinalIgnoreCase);

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
                if (alreadySpentForChannel >= channelTargetSpend[shareTarget.Channel])
                {
                    continue;
                }

                var nextCandidate = candidates
                    .Where(candidate => MatchesChannel(candidate.MediaType, shareTarget.Channel))
                    .Where(candidate => candidate.Cost > 0m)
                    .Where(candidate => !usedSourceIds.Contains(candidate.SourceId))
                    .Where(candidate => spentTotal + candidate.Cost <= request.SelectedBudget)
                    .OrderBy(candidate => GetStationSelectionCount(result, candidate, shareTarget.Channel))
                    .ThenByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.Cost)
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

        FillBudgetGap(result, candidates, request.SelectedBudget, request.MaxMediaItems);

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

        // Exact-fill backtracking can become expensive when candidate pools are large.
        // Guard it to keep recommendation generation responsive in production traffic.
        var shouldAttemptExactFill = fillCandidates.Count <= 40;
        var maxDepth = shouldAttemptExactFill
            ? Math.Min(4, Math.Max(2, maxItems.GetValueOrDefault(result.Count + 2)))
            : 0;
        var exactFill = shouldAttemptExactFill
            ? TryBuildExactFill(fillCandidates, remaining, maxDepth, maxItems, result)
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

    private static bool HasTargetMix(CampaignPlanningRequest request)
    {
        return request.TargetRadioShare.GetValueOrDefault() > 0
            || request.TargetOohShare.GetValueOrDefault() > 0
            || request.TargetTvShare.GetValueOrDefault() > 0
            || request.TargetDigitalShare.GetValueOrDefault() > 0;
    }

    private List<(string Channel, int Share)> GetRequestedShares(CampaignPlanningRequest request)
    {
        var shares = new List<(string Channel, int Share)>();

        var radio = _policyService.GetTargetShare("radio", request);
        if (radio.HasValue && radio.Value > 0)
        {
            shares.Add(("radio", radio.Value));
        }

        var ooh = _policyService.GetTargetShare("ooh", request);
        if (ooh.HasValue && ooh.Value > 0)
        {
            shares.Add(("ooh", ooh.Value));
        }

        var digital = _policyService.GetTargetShare("digital", request);
        if (digital.HasValue && digital.Value > 0)
        {
            shares.Add(("digital", digital.Value));
        }

        var tv = _policyService.GetTargetShare("tv", request);
        if (tv.HasValue && tv.Value > 0)
        {
            shares.Add(("tv", tv.Value));
        }

        var explicitTotal = shares.Sum(entry => entry.Share);
        var hasExplicitTvShare = tv.HasValue && tv.Value > 0;
        var includeTv = request.PreferredMediaTypes
            .Any(media => string.Equals(media?.Trim(), "tv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(media?.Trim(), "television", StringComparison.OrdinalIgnoreCase));
        var tvShare = Math.Max(0, 100 - explicitTotal);
        if (!hasExplicitTvShare && includeTv && tvShare > 0)
        {
            shares.Add(("tv", tvShare));
        }

        return shares;
    }

    private static bool MatchesChannel(string? mediaType, string requestedChannel)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return string.Equals(mediaType.Trim(), requestedChannel.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int GetStationSelectionCount(
        IReadOnlyList<PlannedItem> currentPlan,
        InventoryCandidate candidate,
        string requestedChannel)
    {
        if (!string.Equals(requestedChannel, "tv", StringComparison.OrdinalIgnoreCase))
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
}

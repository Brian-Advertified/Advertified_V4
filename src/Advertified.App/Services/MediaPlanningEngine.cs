using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class MediaPlanningEngine : IMediaPlanningEngine
{
    private const decimal MinimumAcceptableBudgetUtilizationRatio = 0.60m;
    private readonly IPlanningCandidateLoader _candidateLoader;
    private readonly IPlanningEligibilityService _eligibilityService;
    private readonly IRecommendationPlanBuilder _planBuilder;
    private readonly IRecommendationExplainabilityService _explainabilityService;

    public MediaPlanningEngine(
        IPlanningCandidateLoader candidateLoader,
        IPlanningEligibilityService eligibilityService,
        IRecommendationPlanBuilder planBuilder,
        IRecommendationExplainabilityService explainabilityService)
    {
        _candidateLoader = candidateLoader;
        _eligibilityService = eligibilityService;
        _planBuilder = planBuilder;
        _explainabilityService = explainabilityService;
    }

    public MediaPlanningEngine(IPlanningInventoryRepository repository, PlanningPolicySnapshotProvider snapshotProvider)
        : this(
            new PlanningCandidateLoader(repository),
            new PlanningEligibilityService(new PlanningPolicyService(snapshotProvider)),
            new RecommendationPlanBuilder(new PlanningPolicyService(snapshotProvider)),
            new RecommendationExplainabilityService(
                new PlanningScoreService(new PlanningPolicyService(snapshotProvider)),
                new PlanningPolicyService(snapshotProvider)))
    {
    }

    public async Task<RecommendationResult> GenerateAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var planningPasses = BuildPlanningPasses(request);
        RecommendationResult? bestResult = null;

        foreach (var planningPass in planningPasses)
        {
            var passResult = await GenerateForRequestAsync(planningPass.Request, planningPass.FallbackFlags, cancellationToken);
            if (bestResult is null || IsBetterResult(passResult, bestResult))
            {
                bestResult = passResult;
            }

            if (MeetsBudgetUtilizationTarget(passResult, planningPass.Request))
            {
                break;
            }
        }

        return bestResult ?? await GenerateForRequestAsync(NormalizeEngineRequest(request), Array.Empty<string>(), cancellationToken);
    }

    private static CampaignPlanningRequest NormalizeEngineRequest(CampaignPlanningRequest request)
    {
        return new CampaignPlanningRequest
        {
            CampaignId = request.CampaignId,
            SelectedBudget = request.SelectedBudget,
            GeographyScope = request.GeographyScope,
            Provinces = request.Provinces.ToList(),
            Cities = request.Cities.ToList(),
            Suburbs = request.Suburbs.ToList(),
            Areas = request.Areas.ToList(),
            PreferredMediaTypes = NormalizeChannels(request.PreferredMediaTypes),
            ExcludedMediaTypes = NormalizeChannels(request.ExcludedMediaTypes),
            TargetLanguages = request.TargetLanguages.ToList(),
            TargetLsmMin = request.TargetLsmMin,
            TargetLsmMax = request.TargetLsmMax,
            OpenToUpsell = request.OpenToUpsell,
            AdditionalBudget = request.AdditionalBudget,
            MaxMediaItems = request.MaxMediaItems,
            TargetRadioShare = request.TargetRadioShare,
            TargetOohShare = request.TargetOohShare,
            TargetTvShare = request.TargetTvShare,
            TargetDigitalShare = request.TargetDigitalShare
        };
    }

    private static List<string> NormalizeChannels(IEnumerable<string> channels)
    {
        return channels
            .Select(NormalizeChannel)
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<RecommendationResult> GenerateForRequestAsync(
        CampaignPlanningRequest request,
        IReadOnlyCollection<string> passFallbackFlags,
        CancellationToken cancellationToken)
    {
        var allCandidates = await _candidateLoader.LoadCandidatesAsync(request, cancellationToken);
        var policyOutcome = _eligibilityService.FilterEligibleCandidates(allCandidates, request);
        var eligibleCandidates = policyOutcome.Candidates;

        foreach (var candidate in eligibleCandidates)
        {
            var analysis = _explainabilityService.AnalyzeCandidate(candidate, request);
            candidate.Score = analysis.Score;
            candidate.Metadata["selectionReasons"] = analysis.SelectionReasons;
            candidate.Metadata["policyFlags"] = analysis.PolicyFlags;
            candidate.Metadata["confidenceScore"] = analysis.ConfidenceScore;
        }

        var scored = eligibleCandidates
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Cost)
            .ToList();

        var basePlan = _planBuilder.BuildPlan(scored, request, diversify: true);
        var recommendedPlan = _planBuilder.BuildPlan(scored, request, diversify: false);
        EnsureRequiredChannelCoverage(recommendedPlan, scored, request, GetRequiredEngineChannels(request));

        var upsellBudget = request.OpenToUpsell
            ? request.SelectedBudget + (request.AdditionalBudget ?? 0m)
            : request.SelectedBudget;

        var upsells = request.OpenToUpsell && upsellBudget > request.SelectedBudget
            ? _planBuilder.BuildUpsells(scored, recommendedPlan, upsellBudget - recommendedPlan.Sum(x => x.TotalCost))
            : new List<PlannedItem>();

        var fallbackFlags = new List<string>(policyOutcome.FallbackFlags);
        fallbackFlags.AddRange(passFallbackFlags);

        if (eligibleCandidates.Count == 0)
        {
            fallbackFlags.Add("inventory_insufficient");
        }

        if (recommendedPlan.Count == 0)
        {
            fallbackFlags.Add("no_recommendation_generated");
        }

        fallbackFlags.AddRange(_explainabilityService.GetPreferredMediaFallbackFlags(request, recommendedPlan));

        return new RecommendationResult
        {
            BasePlan = basePlan,
            RecommendedPlan = recommendedPlan,
            Upsells = upsells,
            FallbackFlags = fallbackFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ManualReviewRequired = fallbackFlags.Count > 0,
            Rationale = _explainabilityService.BuildRationale(basePlan, recommendedPlan, request)
        };
    }

    private static IReadOnlyList<string> GetRequiredEngineChannels(CampaignPlanningRequest request)
    {
        var requiredChannels = new List<string>();

        AddRequiredChannel(requiredChannels, "radio", request.TargetRadioShare);
        AddRequiredChannel(requiredChannels, "ooh", request.TargetOohShare);
        AddRequiredChannel(requiredChannels, "tv", request.TargetTvShare);
        AddRequiredChannel(requiredChannels, "digital", request.TargetDigitalShare);

        return requiredChannels;
    }

    private static void AddRequiredChannel(List<string> channels, string channel, int? share)
    {
        if (share.GetValueOrDefault() > 0)
        {
            channels.Add(channel);
        }
    }

    private static IReadOnlyList<PlanningPass> BuildPlanningPasses(CampaignPlanningRequest request)
    {
        var normalizedRequest = NormalizeEngineRequest(request);
        var passes = new List<PlanningPass> { new(normalizedRequest, Array.Empty<string>()) };

        var broaderRequest = BuildBroaderGeographyRequest(normalizedRequest);
        if (!AreEquivalentRequests(normalizedRequest, broaderRequest))
        {
            passes.Add(new(broaderRequest, new[] { "geography_relaxed" }));
        }

        return passes;
    }

    private static CampaignPlanningRequest BuildBroaderGeographyRequest(CampaignPlanningRequest request)
    {
        return new CampaignPlanningRequest
        {
            CampaignId = request.CampaignId,
            SelectedBudget = request.SelectedBudget,
            GeographyScope = request.GeographyScope,
            Provinces = request.Provinces.ToList(),
            Cities = new List<string>(),
            Suburbs = new List<string>(),
            Areas = new List<string>(),
            PreferredMediaTypes = request.PreferredMediaTypes.ToList(),
            ExcludedMediaTypes = request.ExcludedMediaTypes.ToList(),
            TargetLanguages = request.TargetLanguages.ToList(),
            TargetLsmMin = request.TargetLsmMin,
            TargetLsmMax = request.TargetLsmMax,
            OpenToUpsell = request.OpenToUpsell,
            AdditionalBudget = request.AdditionalBudget,
            MaxMediaItems = request.MaxMediaItems,
            TargetRadioShare = request.TargetRadioShare,
            TargetOohShare = request.TargetOohShare,
            TargetTvShare = request.TargetTvShare,
            TargetDigitalShare = request.TargetDigitalShare
        };
    }

    private static bool AreEquivalentRequests(CampaignPlanningRequest left, CampaignPlanningRequest right)
    {
        return left.Provinces.SequenceEqual(right.Provinces, StringComparer.OrdinalIgnoreCase)
            && left.Cities.SequenceEqual(right.Cities, StringComparer.OrdinalIgnoreCase)
            && left.Suburbs.SequenceEqual(right.Suburbs, StringComparer.OrdinalIgnoreCase)
            && left.Areas.SequenceEqual(right.Areas, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsBetterResult(RecommendationResult candidate, RecommendationResult currentBest)
    {
        if (candidate.RecommendedPlanTotal != currentBest.RecommendedPlanTotal)
        {
            return candidate.RecommendedPlanTotal > currentBest.RecommendedPlanTotal;
        }

        if (candidate.ManualReviewRequired != currentBest.ManualReviewRequired)
        {
            return !candidate.ManualReviewRequired;
        }

        return candidate.FallbackFlags.Count < currentBest.FallbackFlags.Count;
    }

    private static bool MeetsBudgetUtilizationTarget(RecommendationResult result, CampaignPlanningRequest request)
    {
        if (request.SelectedBudget <= 0m)
        {
            return true;
        }

        return result.RecommendedPlanTotal >= request.SelectedBudget * MinimumAcceptableBudgetUtilizationRatio;
    }

    private static void EnsureRequiredChannelCoverage(
        List<PlannedItem> recommendedPlan,
        IReadOnlyList<InventoryCandidate> scoredCandidates,
        CampaignPlanningRequest request,
        IReadOnlyCollection<string> requiredChannels)
    {
        if (recommendedPlan.Count == 0 || requiredChannels.Count == 0 || scoredCandidates.Count == 0)
        {
            return;
        }

        var selectedChannels = recommendedPlan
            .Select(item => NormalizeChannel(item.MediaType))
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredSet = requiredChannels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in requiredChannels)
        {
            if (selectedChannels.Contains(channel))
            {
                continue;
            }

            var candidate = scoredCandidates
                .Where(item => NormalizeChannel(item.MediaType).Equals(channel, StringComparison.OrdinalIgnoreCase))
                .Where(item => item.Cost > 0m)
                .Where(item => item.Cost <= request.SelectedBudget)
                .Where(item => recommendedPlan.All(line => line.SourceId != item.SourceId))
                .OrderBy(item => item.Cost)
                .ThenByDescending(item => item.Score)
                .FirstOrDefault();

            if (candidate is null)
            {
                continue;
            }

            var total = recommendedPlan.Sum(item => item.TotalCost);
            var remaining = request.SelectedBudget - total;
            var canAppend = !request.MaxMediaItems.HasValue || recommendedPlan.Count < request.MaxMediaItems.Value;

            if (canAppend && candidate.Cost <= remaining)
            {
                recommendedPlan.Add(ToPlannedItem(candidate));
                selectedChannels.Add(channel);
                continue;
            }

            var requiredReduction = candidate.Cost - remaining;
            if (requiredReduction <= 0m)
            {
                recommendedPlan.Add(ToPlannedItem(candidate));
                selectedChannels = recommendedPlan
                    .Select(item => NormalizeChannel(item.MediaType))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var removable = BuildRemovableLineIndexes(recommendedPlan, requiredSet)
                .ToList();
            var indexesToRemove = new List<int>();
            var freed = 0m;

            foreach (var index in removable)
            {
                indexesToRemove.Add(index);
                freed += recommendedPlan[index].TotalCost;
                if (freed >= requiredReduction)
                {
                    break;
                }
            }

            if (freed >= requiredReduction)
            {
                foreach (var index in indexesToRemove.OrderByDescending(value => value))
                {
                    recommendedPlan.RemoveAt(index);
                }

                recommendedPlan.Add(ToPlannedItem(candidate));
                selectedChannels = recommendedPlan
                    .Select(item => NormalizeChannel(item.MediaType))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static IEnumerable<int> BuildRemovableLineIndexes(List<PlannedItem> plan, HashSet<string> preferredSet)
    {
        var channelCounts = plan
            .Select(item => NormalizeChannel(item.MediaType))
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .GroupBy(channel => channel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var ordered = plan
            .Select((item, index) => new
            {
                Index = index,
                Item = item,
                Channel = NormalizeChannel(item.MediaType)
            })
            .OrderBy(entry => preferredSet.Contains(entry.Channel)) // remove non-preferred channels first
            .ThenBy(entry => entry.Item.Score)
            .ThenByDescending(entry => entry.Item.TotalCost)
            .ToList();

        var remainingCounts = new Dictionary<string, int>(channelCounts, StringComparer.OrdinalIgnoreCase);
        var removable = new List<int>(ordered.Count);

        foreach (var entry in ordered)
        {
            if (string.IsNullOrWhiteSpace(entry.Channel))
            {
                removable.Add(entry.Index);
                continue;
            }

            if (!preferredSet.Contains(entry.Channel))
            {
                removable.Add(entry.Index);
                if (remainingCounts.TryGetValue(entry.Channel, out var nonPreferredCount) && nonPreferredCount > 0)
                {
                    remainingCounts[entry.Channel] = nonPreferredCount - 1;
                }

                continue;
            }

            if (remainingCounts.TryGetValue(entry.Channel, out var preferredCount) && preferredCount > 1)
            {
                removable.Add(entry.Index);
                remainingCounts[entry.Channel] = preferredCount - 1;
            }
        }

        return removable;
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

    private static string NormalizeChannel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "television" => "tv",
            _ => normalized
        };
    }

    private sealed record PlanningPass(CampaignPlanningRequest Request, IReadOnlyCollection<string> FallbackFlags);
}

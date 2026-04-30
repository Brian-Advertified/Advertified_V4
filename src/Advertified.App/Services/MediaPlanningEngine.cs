using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class MediaPlanningEngine : IMediaPlanningEngine
{
    private const decimal MinimumAcceptableBudgetUtilizationRatio = 0.60m;
    private readonly IPlanningCandidateLoader _candidateLoader;
    private readonly IPlanningEligibilityService _eligibilityService;
    private readonly IRecommendationPlanBuilder _planBuilder;
    private readonly IRecommendationExplainabilityService _explainabilityService;
    private readonly IPlanningPolicyService _policyService;
    private readonly IBroadcastLanguagePriorityService _broadcastLanguagePriorityService;

    public MediaPlanningEngine(
        IPlanningCandidateLoader candidateLoader,
        IPlanningEligibilityService eligibilityService,
        IRecommendationPlanBuilder planBuilder,
        IRecommendationExplainabilityService explainabilityService,
        IPlanningPolicyService policyService,
        IBroadcastLanguagePriorityService broadcastLanguagePriorityService)
    {
        _candidateLoader = candidateLoader;
        _eligibilityService = eligibilityService;
        _planBuilder = planBuilder;
        _explainabilityService = explainabilityService;
        _policyService = policyService;
        _broadcastLanguagePriorityService = broadcastLanguagePriorityService;
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
        var clone = request.DeepClone();
        clone.PreferredMediaTypes = NormalizeChannels(request.PreferredMediaTypes);
        clone.ExcludedMediaTypes = NormalizeChannels(request.ExcludedMediaTypes);
        return clone;
    }

    private static List<string> NormalizeChannels(IEnumerable<string> channels)
    {
        return channels
            .SelectMany(PlanningChannelSupport.ExpandRequestedChannel)
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<RecommendationResult> GenerateForRequestAsync(
        CampaignPlanningRequest request,
        IReadOnlyCollection<string> passFallbackFlags,
        CancellationToken cancellationToken)
    {
        var preparedRequest = await PrepareRequestAsync(request, cancellationToken);
        var allCandidates = await _candidateLoader.LoadCandidatesAsync(preparedRequest, cancellationToken);
        var policyOutcome = _eligibilityService.FilterEligibleCandidates(allCandidates, preparedRequest);
        var eligibleCandidates = policyOutcome.Candidates;

        foreach (var candidate in eligibleCandidates)
        {
            var analysis = _explainabilityService.AnalyzeCandidate(candidate, preparedRequest);
            var commercialAdjustment = ResolveCommercialScoreAdjustment(candidate.Metadata);
            candidate.Score = Math.Max(0m, analysis.Score + commercialAdjustment);
            candidate.Metadata["selectionReasons"] = MergeSelectionReasons(
                analysis.SelectionReasons,
                GetMetadataString(candidate.Metadata, "commercialExplanation"));
            candidate.Metadata["policyFlags"] = analysis.PolicyFlags;
            candidate.Metadata["confidenceScore"] = analysis.ConfidenceScore;
        }

        var scored = eligibleCandidates
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Cost)
            .ToList();

        var basePlan = _planBuilder.BuildPlan(scored, preparedRequest, diversify: true);
        var recommendedPlan = _planBuilder.BuildPlan(scored, preparedRequest, diversify: false);
        EnsureRequiredChannelCoverage(recommendedPlan, scored, preparedRequest, _policyService.GetRequiredChannels(preparedRequest));

        var upsellBudget = preparedRequest.OpenToUpsell
            ? preparedRequest.SelectedBudget + (preparedRequest.AdditionalBudget ?? 0m)
            : preparedRequest.SelectedBudget;

        var upsells = preparedRequest.OpenToUpsell && upsellBudget > preparedRequest.SelectedBudget
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

        fallbackFlags.AddRange(_explainabilityService.GetPreferredMediaFallbackFlags(preparedRequest, recommendedPlan));

        var distinctFallbackFlags = fallbackFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var manualReviewRequired = distinctFallbackFlags.Count > 0;

        return new RecommendationResult
        {
            BasePlan = basePlan,
            RecommendedPlan = recommendedPlan,
            Upsells = upsells,
            FallbackFlags = distinctFallbackFlags,
            ManualReviewRequired = manualReviewRequired,
            Rationale = _explainabilityService.BuildRationale(basePlan, recommendedPlan, preparedRequest),
            RunTrace = BuildRunTrace(preparedRequest, allCandidates, eligibleCandidates, policyOutcome.Rejections, recommendedPlan, upsells, distinctFallbackFlags, manualReviewRequired)
        };
    }

    private async Task<CampaignPlanningRequest> PrepareRequestAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var orderedLanguages = await _broadcastLanguagePriorityService.OrderRequestedLanguagesAsync(request.TargetLanguages, cancellationToken);
        var clone = request.DeepClone();
        clone.TargetLanguages = orderedLanguages.ToList();
        return clone;
    }

    private static IReadOnlyList<PlanningPass> BuildPlanningPasses(CampaignPlanningRequest request)
    {
        var normalizedRequest = NormalizeEngineRequest(request);
        var passes = new List<PlanningPass> { new(normalizedRequest, Array.Empty<string>()) };
        if (HasExplicitGeographyConstraints(normalizedRequest))
        {
            return passes;
        }

        var broaderRequest = BuildBroaderGeographyRequest(normalizedRequest);
        if (!AreEquivalentRequests(normalizedRequest, broaderRequest))
        {
            passes.Add(new(broaderRequest, new[] { "geography_relaxed" }));
        }

        return passes;
    }

    private static bool HasExplicitGeographyConstraints(CampaignPlanningRequest request)
    {
        return request.Cities.Count > 0
            || request.Suburbs.Count > 0
            || request.Areas.Count > 0
            || request.TargetLatitude.HasValue
            || request.TargetLongitude.HasValue
            || request.Targeting?.Cities.Count > 0
            || request.Targeting?.Suburbs.Count > 0
            || request.Targeting?.Areas.Count > 0
            || request.Targeting?.Latitude.HasValue == true
            || request.Targeting?.Longitude.HasValue == true;
    }

    private static CampaignPlanningRequest BuildBroaderGeographyRequest(CampaignPlanningRequest request)
    {
        var clone = request.DeepClone();
        clone.Cities = new List<string>();
        clone.Suburbs = new List<string>();
        clone.Areas = new List<string>();
        clone.TargetLocationSource = "geography_relaxed";
        if (clone.Targeting is not null)
        {
            clone.Targeting.Cities = new List<string>();
            clone.Targeting.Suburbs = new List<string>();
            clone.Targeting.Areas = new List<string>();
            clone.Targeting.Source = "geography_relaxed";
        }

        return clone;
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

    private void EnsureRequiredChannelCoverage(
        List<PlannedItem> recommendedPlan,
        IReadOnlyList<InventoryCandidate> scoredCandidates,
        CampaignPlanningRequest request,
        IReadOnlyCollection<string> requiredChannels)
    {
        if (recommendedPlan.Count == 0
            || requiredChannels.Count == 0
            || scoredCandidates.Count == 0
            || HasStructuredChannelPlan(request))
        {
            return;
        }

        var selectedChannels = recommendedPlan
            .Select(item => NormalizeRequestedChannel(item.MediaType))
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orderedRequiredChannels = requiredChannels
            .Select(NormalizeRequestedChannel)
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(channel => GetChannelPriorityWeight(channel, request))
            .ToArray();
        var requiredSet = orderedRequiredChannels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in orderedRequiredChannels)
        {
            if (selectedChannels.Contains(channel))
            {
                continue;
            }

            var candidate = scoredCandidates
                .Where(item => PlanningChannelSupport.MatchesRequestedChannel(item.MediaType, channel))
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
            }
        }
    }

    private bool HasStructuredChannelPlan(CampaignPlanningRequest request)
    {
        return request.BudgetAllocation?.CompositeAllocations.Count > 0
            || _policyService.GetRequestedChannelShares(request).Count > 0;
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
        return PlanningChannelSupport.NormalizeChannel(normalized);
    }

    private static string NormalizeRequestedChannel(string? raw)
    {
        var normalized = NormalizeChannel(raw);
        return PlanningChannelSupport.IsOohFamilyChannel(normalized)
            ? PlanningChannelSupport.OohAlias
            : normalized;
    }

    private int GetChannelPriorityWeight(string? raw, CampaignPlanningRequest request)
    {
        var normalized = NormalizeRequestedChannel(raw);
        return _policyService.GetTargetShare(normalized, request).GetValueOrDefault();
    }

    private static RecommendationRunTrace BuildRunTrace(
        CampaignPlanningRequest request,
        IReadOnlyList<InventoryCandidate> allCandidates,
        IReadOnlyList<InventoryCandidate> eligibleCandidates,
        IReadOnlyList<PlanningCandidateRejection> rejections,
        IReadOnlyList<PlannedItem> recommendedPlan,
        IReadOnlyList<PlannedItem> upsells,
        IReadOnlyList<string> fallbackFlags,
        bool manualReviewRequired)
    {
        return new RecommendationRunTrace
        {
            RequestSnapshot = BuildRequestSnapshot(request),
            CandidateCounts = BuildCandidateCounts(allCandidates, eligibleCandidates, recommendedPlan, upsells),
            RejectedCandidates = rejections
                .Select(rejection => new RecommendationRejectedCandidateTrace
                {
                    Stage = rejection.Stage,
                    Reason = rejection.Reason,
                    SourceId = rejection.SourceId,
                    DisplayName = rejection.DisplayName,
                    MediaType = rejection.MediaType
                })
                .ToList(),
            SelectedItems = recommendedPlan
                .Select(item => BuildSelectedItemTrace(item, isUpsell: false))
                .Concat(upsells.Select(item => BuildSelectedItemTrace(item, isUpsell: true)))
                .ToList(),
            FallbackFlags = fallbackFlags.ToList(),
            ManualReviewRequired = manualReviewRequired
        };
    }

    private static CampaignPlanningRequestSnapshot BuildRequestSnapshot(CampaignPlanningRequest request)
    {
        return new CampaignPlanningRequestSnapshot
        {
            CampaignId = request.CampaignId,
            SelectedBudget = request.SelectedBudget,
            BusinessLocation = request.BusinessLocation is null ? null : new CampaignBusinessLocationSnapshot
            {
                Label = request.BusinessLocation.Label,
                Area = request.BusinessLocation.Area,
                City = request.BusinessLocation.City,
                Province = request.BusinessLocation.Province,
                Latitude = request.BusinessLocation.Latitude,
                Longitude = request.BusinessLocation.Longitude,
                Source = request.BusinessLocation.Source,
                Precision = request.BusinessLocation.Precision,
                IsResolved = request.BusinessLocation.IsResolved
            },
            Targeting = request.Targeting is null ? null : new CampaignTargetingProfileSnapshot
            {
                Scope = request.Targeting.Scope,
                Label = request.Targeting.Label,
                City = request.Targeting.City,
                Province = request.Targeting.Province,
                Latitude = request.Targeting.Latitude,
                Longitude = request.Targeting.Longitude,
                Source = request.Targeting.Source,
                Precision = request.Targeting.Precision,
                Provinces = request.Targeting.Provinces.ToList(),
                Cities = request.Targeting.Cities.ToList(),
                Suburbs = request.Targeting.Suburbs.ToList(),
                Areas = request.Targeting.Areas.ToList(),
                PriorityAreas = request.Targeting.PriorityAreas.ToList(),
                Exclusions = request.Targeting.Exclusions.ToList()
            },
            BudgetAllocation = request.BudgetAllocation is null ? null : new PlanningBudgetAllocationSnapshot
            {
                ChannelPolicyKey = request.BudgetAllocation.ChannelPolicyKey,
                GeoPolicyKey = request.BudgetAllocation.GeoPolicyKey,
                AudienceSegment = request.BudgetAllocation.AudienceSegment,
                ChannelAllocations = request.BudgetAllocation.ChannelAllocations
                    .Select(static allocation => new PlanningChannelAllocationSnapshot
                    {
                        Channel = allocation.Channel,
                        Weight = allocation.Weight,
                        Amount = allocation.Amount
                    })
                    .ToList(),
                GeoAllocations = request.BudgetAllocation.GeoAllocations
                    .Select(static allocation => new PlanningGeoAllocationSnapshot
                    {
                        Bucket = allocation.Bucket,
                        Weight = allocation.Weight,
                        Amount = allocation.Amount,
                        RadiusKm = allocation.RadiusKm
                    })
                    .ToList(),
                CompositeAllocations = request.BudgetAllocation.CompositeAllocations
                    .Select(static allocation => new PlanningAllocationLineSnapshot
                    {
                        Channel = allocation.Channel,
                        Bucket = allocation.Bucket,
                        Weight = allocation.Weight,
                        Amount = allocation.Amount,
                        RadiusKm = allocation.RadiusKm
                    })
                    .ToList()
            },
            Objective = request.Objective,
            Industry = request.Industry,
            BusinessStage = request.BusinessStage,
            MonthlyRevenueBand = request.MonthlyRevenueBand,
            SalesModel = request.SalesModel,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DurationWeeks = request.DurationWeeks,
            ChannelFlights = request.ChannelFlights
                .Select(flight => new CampaignChannelFlightSnapshot
                {
                    Channel = flight.Channel,
                    StartDate = flight.StartDate,
                    EndDate = flight.EndDate,
                    DurationWeeks = flight.DurationWeeks,
                    DurationMonths = flight.DurationMonths,
                    Priority = flight.Priority,
                    Notes = flight.Notes
                })
                .ToList(),
            GeographyScope = request.GeographyScope,
            Provinces = request.Provinces.ToList(),
            Cities = request.Cities.ToList(),
            Suburbs = request.Suburbs.ToList(),
            Areas = request.Areas.ToList(),
            TargetLocationLabel = request.TargetLocationLabel,
            TargetLocationCity = request.TargetLocationCity,
            TargetLocationProvince = request.TargetLocationProvince,
            TargetLocationSource = request.TargetLocationSource,
            TargetLocationPrecision = request.TargetLocationPrecision,
            PreferredMediaTypes = request.PreferredMediaTypes.ToList(),
            ExcludedMediaTypes = request.ExcludedMediaTypes.ToList(),
            TargetLanguages = request.TargetLanguages.ToList(),
            TargetAgeMin = request.TargetAgeMin,
            TargetAgeMax = request.TargetAgeMax,
            TargetGender = request.TargetGender,
            TargetInterests = request.TargetInterests.ToList(),
            TargetAudienceNotes = request.TargetAudienceNotes,
            CustomerType = request.CustomerType,
            BuyingBehaviour = request.BuyingBehaviour,
            DecisionCycle = request.DecisionCycle,
            PricePositioning = request.PricePositioning,
            AverageCustomerSpendBand = request.AverageCustomerSpendBand,
            GrowthTarget = request.GrowthTarget,
            UrgencyLevel = request.UrgencyLevel,
            AudienceClarity = request.AudienceClarity,
            ValuePropositionFocus = request.ValuePropositionFocus,
            TargetLsmMin = request.TargetLsmMin,
            TargetLsmMax = request.TargetLsmMax,
            MustHaveAreas = request.MustHaveAreas.ToList(),
            ExcludedAreas = request.ExcludedAreas.ToList(),
            OpenToUpsell = request.OpenToUpsell,
            AdditionalBudget = request.AdditionalBudget,
            MaxMediaItems = request.MaxMediaItems,
            TargetRadioShare = request.TargetRadioShare,
            TargetOohShare = request.TargetOohShare,
            TargetTvShare = request.TargetTvShare,
            TargetDigitalShare = request.TargetDigitalShare,
            TargetNewspaperShare = request.TargetNewspaperShare,
            TargetLatitude = request.TargetLatitude,
            TargetLongitude = request.TargetLongitude
        };
    }

    private static List<RecommendationTraceCount> BuildCandidateCounts(
        IReadOnlyList<InventoryCandidate> allCandidates,
        IReadOnlyList<InventoryCandidate> eligibleCandidates,
        IReadOnlyList<PlannedItem> recommendedPlan,
        IReadOnlyList<PlannedItem> upsells)
    {
        var counts = new List<RecommendationTraceCount>();
        counts.AddRange(BuildStageCounts(
            "loaded",
            allCandidates.Select(candidate => NormalizeChannel(candidate.MediaType))));
        counts.AddRange(BuildStageCounts(
            "eligible",
            eligibleCandidates.Select(candidate => NormalizeChannel(candidate.MediaType))));
        counts.AddRange(BuildStageCounts(
            "selected",
            recommendedPlan.Select(item => NormalizeChannel(item.MediaType))));
        counts.AddRange(BuildStageCounts(
            "upsell",
            upsells.Select(item => NormalizeChannel(item.MediaType))));
        return counts;
    }

    private static IEnumerable<RecommendationTraceCount> BuildStageCounts(string stage, IEnumerable<string> mediaTypes)
    {
        return mediaTypes
            .Where(mediaType => !string.IsNullOrWhiteSpace(mediaType))
            .GroupBy(mediaType => mediaType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RecommendationTraceCount
            {
                Stage = stage,
                MediaType = group.Key,
                Count = group.Count()
            });
    }

    private static RecommendationSelectedItemTrace BuildSelectedItemTrace(PlannedItem item, bool isUpsell)
    {
        return new RecommendationSelectedItemTrace
        {
            SourceId = item.SourceId,
            DisplayName = item.DisplayName,
            MediaType = NormalizeChannel(item.MediaType),
            Score = item.Score,
            TotalCost = item.TotalCost,
            IsUpsell = isUpsell,
            SelectionReasons = GetMetadataArray(item.Metadata, "selectionReasons"),
            PolicyFlags = GetMetadataArray(item.Metadata, "policyFlags"),
            ConfidenceScore = GetMetadataValue(item.Metadata, "confidenceScore")
        };
    }

    private static IReadOnlyList<string> GetMetadataArray(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return Array.Empty<string>();
        }

        return value switch
        {
            IEnumerable<string> strings => strings.Where(static item => !string.IsNullOrWhiteSpace(item)).ToArray(),
            string text when !string.IsNullOrWhiteSpace(text) => new[] { text },
            _ => new[] { value.ToString()! }.Where(static item => !string.IsNullOrWhiteSpace(item)).ToArray()
        };
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static decimal ResolveCommercialScoreAdjustment(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue("durationFitScore", out var fitScoreValue) || fitScoreValue is null)
        {
            return 0m;
        }

        if (!decimal.TryParse(fitScoreValue.ToString(), out var fitScore))
        {
            return 0m;
        }

        return decimal.Round((fitScore - 50m) / 5m, 2, MidpointRounding.AwayFromZero);
    }

    private static string[] MergeSelectionReasons(IEnumerable<string> existing, string? commercialExplanation)
    {
        var merged = existing
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Select(reason => reason.Trim())
            .ToList();

        if (!string.IsNullOrWhiteSpace(commercialExplanation))
        {
            merged.Add(commercialExplanation.Trim());
        }

        return merged
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetMetadataString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private sealed record PlanningPass(CampaignPlanningRequest Request, IReadOnlyCollection<string> FallbackFlags);

}

using System.Globalization;
using System.Text.Json;
using Advertified.App.Campaigns;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Configuration;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services;

public sealed class CampaignRecommendationService : ICampaignRecommendationService
{
    private const string FallbackFlagsMarker = "Fallback flags:";
    private const string ManualReviewMarker = "Manual review required:";
    private const string TierBoundaryToleranceFlag = "tier_boundary_tolerance_used";
    private const string TierRecoveryUsedFlag = "tier_recovery_used";
    private const string TierRecoveryRelaxedMaxItemsFlag = "tier_recovery_relaxed_max_media_items";
    private const string TierRecoveryRelaxedMixFlag = "tier_recovery_relaxed_channel_mix";
    private const decimal ProposalTierMaxBudgetToleranceRatio = 0.02m;
    private const decimal ProposalTierSpanToleranceRatio = 0.15m;
    private const decimal ProposalTierRoundingSlack = 1000m;
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly IMediaPlanningEngine _planningEngine;
    private readonly ICampaignReasoningService _campaignReasoningService;
    private readonly PlanningPolicySnapshotProvider _policySnapshotProvider;
    private readonly IPlanningPolicyService _policyService;
    private readonly IPlanningRequestFactory _planningRequestFactory;

    internal CampaignRecommendationService(
        AppDbContext db,
        IMediaPlanningEngine planningEngine,
        ICampaignReasoningService campaignReasoningService,
        PlanningPolicySnapshotProvider policySnapshotProvider,
        IPlanningPolicyService policyService)
    {
        _db = db;
        _planningEngine = planningEngine;
        _campaignReasoningService = campaignReasoningService;
        _policySnapshotProvider = policySnapshotProvider;
        _policyService = policyService;
        _planningRequestFactory = new PlanningRequestFactory(new NullPlanningTargetResolver(), new NullBusinessLocationResolver(), new NullPlanningBudgetAllocationService());
    }

    public CampaignRecommendationService(
        AppDbContext db,
        IMediaPlanningEngine planningEngine,
        ICampaignReasoningService campaignReasoningService)
    {
        _db = db;
        _planningEngine = planningEngine;
        _campaignReasoningService = campaignReasoningService;
        _policySnapshotProvider = new PlanningPolicySnapshotProvider(new PlanningPolicyOptions());
        _policyService = new PlanningPolicyService(_policySnapshotProvider);
        _planningRequestFactory = new PlanningRequestFactory(new NullPlanningTargetResolver(), new NullBusinessLocationResolver(), new NullPlanningBudgetAllocationService());
    }

    [ActivatorUtilitiesConstructor]
    public CampaignRecommendationService(
        AppDbContext db,
        IMediaPlanningEngine planningEngine,
        ICampaignReasoningService campaignReasoningService,
        IPlanningPolicyService policyService,
        IOptions<PlanningPolicyOptions> planningPolicyOptions,
        IPlanningRequestFactory planningRequestFactory)
    {
        _db = db;
        _planningEngine = planningEngine;
        _campaignReasoningService = campaignReasoningService;
        _policySnapshotProvider = new PlanningPolicySnapshotProvider(planningPolicyOptions.Value);
        _policyService = policyService;
        _planningRequestFactory = planningRequestFactory;
    }

    public async Task<Guid> GenerateAndSaveAsync(Guid campaignId, GenerateRecommendationRequest? request, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var brief = campaign.CampaignBrief
            ?? throw new InvalidOperationException("Campaign brief not found.");
        var packageProfile = await _db.PackageBandProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PackageBandId == campaign.PackageBandId, cancellationToken);

        var now = DateTime.UtcNow;
        var planningRequest = BuildRequest(campaign, brief, request, packageProfile);
        var proposalVariants = BuildProposalVariants(planningRequest, campaign.PackageBand);
        var policySnapshot = _policySnapshotProvider.GetCurrent();
        var inventorySnapshot = await BuildInventorySnapshotAsync(cancellationToken);
        Guid? primaryRecommendationId = null;
        var revisionNumber = RecommendationRevisionSupport.GetNextRevisionNumber(campaign.CampaignRecommendations);

        for (var index = 0; index < proposalVariants.Count; index++)
        {
            var variant = proposalVariants[index];
            var policyContext = _policyService.BuildPolicyContext(variant.Request);
            var recommendationResult = await GenerateRecommendationForVariantAsync(variant, cancellationToken);
            EnsureRecommendationFallsWithinTier(variant, recommendationResult);

            var aiReasoning = await _campaignReasoningService.GenerateAsync(campaign, brief, variant.Request, recommendationResult, cancellationToken);
            var proposalTimestamp = now.AddMilliseconds(index);
            var recommendation = CreateRecommendationEntity(
                campaignId,
                campaign.PlanningMode,
                variant.Key,
                recommendationResult,
                variant.Request,
                aiReasoning,
                proposalTimestamp,
                revisionNumber,
                policySnapshot,
                policyContext,
                inventorySnapshot);

            foreach (var item in recommendationResult.RecommendedPlan)
            {
                recommendation.RecommendationItems.Add(ToRecommendationItem(item, recommendation.Id, proposalTimestamp, isUpsell: false, recommendationResult.RunTrace));
            }

            foreach (var item in recommendationResult.Upsells)
            {
                recommendation.RecommendationItems.Add(ToRecommendationItem(item, recommendation.Id, proposalTimestamp, isUpsell: true, recommendationResult.RunTrace));
            }

            _db.RecommendationRunAudits.Add(CreateRecommendationRunAudit(
                campaignId,
                recommendation,
                recommendationResult,
                variant.Request,
                inventorySnapshot.BatchReferences,
                proposalTimestamp));
            primaryRecommendationId ??= recommendation.Id;
            _db.CampaignRecommendations.Add(recommendation);
        }

        campaign.Status = CampaignStatuses.PlanningInProgress;
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return primaryRecommendationId ?? Guid.Empty;
    }

    private async Task<RecommendationResult> GenerateRecommendationForVariantAsync(ProposalVariant variant, CancellationToken cancellationToken)
    {
        var primary = await _planningEngine.GenerateAsync(variant.Request, cancellationToken);
        if (variant.BudgetBand is null)
        {
            return primary;
        }

        if (primary.RecommendedPlanTotal >= variant.BudgetBand.MinBudget)
        {
            return primary;
        }

        RecommendationResult best = primary;
        foreach (var candidate in BuildTierRecoveryRequests(variant.Request))
        {
            var retried = await _planningEngine.GenerateAsync(candidate.Request, cancellationToken);
            var improved = retried.RecommendedPlanTotal > best.RecommendedPlanTotal;
            if (improved)
            {
                best = retried;
            }

            var withinTier = IsWithinProposalTier(retried.RecommendedPlanTotal, variant.BudgetBand.MinBudget, variant.BudgetBand.MaxBudget)
                || IsWithinProposalTierTolerance(retried.RecommendedPlanTotal, variant.BudgetBand.MinBudget, variant.BudgetBand.MaxBudget);
            if (!withinTier)
            {
                continue;
            }

            MarkTierRecoveryFlags(retried, candidate.RelaxedMaxItems, candidate.RelaxedMix);
            return retried;
        }

        if (variant.BudgetBand.PlanningBudget < variant.BudgetBand.MaxBudget)
        {
            var maxBudgetRequest = CloneRequest(variant.Request);
            maxBudgetRequest.SelectedBudget = variant.BudgetBand.MaxBudget;
            var maxBudgetRetry = await _planningEngine.GenerateAsync(maxBudgetRequest, cancellationToken);
            if (maxBudgetRetry.RecommendedPlanTotal > best.RecommendedPlanTotal)
            {
                best = maxBudgetRetry;
            }

            var withinTier = IsWithinProposalTier(maxBudgetRetry.RecommendedPlanTotal, variant.BudgetBand.MinBudget, variant.BudgetBand.MaxBudget)
                || IsWithinProposalTierTolerance(maxBudgetRetry.RecommendedPlanTotal, variant.BudgetBand.MinBudget, variant.BudgetBand.MaxBudget);
            if (withinTier)
            {
                MarkTierRecoveryFlags(maxBudgetRetry, relaxedMaxItems: false, relaxedMix: false);
                return maxBudgetRetry;
            }
        }

        return best;
    }

    private CampaignPlanningRequest BuildRequest(
        CampaignEntity campaign,
        CampaignBriefEntity brief,
        GenerateRecommendationRequest? request,
        PackageBandProfile? packageProfile)
    {
        return _planningRequestFactory.FromCampaignBrief(campaign, brief, request, packageProfile);
    }

    private static CampaignRecommendation CreateRecommendationEntity(
        Guid campaignId,
        string? planningMode,
        string variantKey,
        RecommendationResult recommendationResult,
        CampaignPlanningRequest planningRequest,
        CampaignReasoningResult? aiReasoning,
        DateTime now,
        int revisionNumber,
        PlanningPolicyOptions policySnapshot,
        PlanningPolicyContext policyContext,
        InventorySnapshot inventorySnapshot)
    {
        return new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RecommendationType = $"{planningMode ?? "ai_assisted"}:{variantKey}",
            GeneratedBy = "system",
            Status = "draft",
            TotalCost = recommendationResult.RecommendedPlanTotal,
            Summary = aiReasoning?.Summary ?? BuildSummary(recommendationResult, planningRequest),
            Rationale = BuildStoredRationale(recommendationResult, aiReasoning?.Rationale),
            RequestSnapshotJson = SerializeAuditJson(recommendationResult.RunTrace?.RequestSnapshot ?? BuildRequestSnapshot(planningRequest)),
            PolicySnapshotJson = SerializeAuditJson(new
            {
                options = policySnapshot,
                context = policyContext
            }),
            InventorySnapshotJson = SerializeAuditJson(inventorySnapshot),
            InventoryBatchRefsJson = SerializeAuditJson(inventorySnapshot.BatchReferences),
            RevisionNumber = revisionNumber,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static RecommendationRunAudit CreateRecommendationRunAudit(
        Guid campaignId,
        CampaignRecommendation recommendation,
        RecommendationResult recommendationResult,
        CampaignPlanningRequest planningRequest,
        IReadOnlyList<InventoryBatchReferenceSnapshot> inventoryBatchReferences,
        DateTime createdAt)
    {
        var selectedChannels = recommendationResult.RecommendedPlan
            .Select(item => item.MediaType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(channel => channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var budgetUtilizationRatio = planningRequest.SelectedBudget <= 0m
            ? 0m
            : Math.Round(recommendationResult.RecommendedPlanTotal / planningRequest.SelectedBudget, 4, MidpointRounding.AwayFromZero);

        return new RecommendationRunAudit
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RecommendationId = recommendation.Id,
            RecommendationType = recommendation.RecommendationType,
            RevisionNumber = recommendation.RevisionNumber,
            RequestSnapshotJson = recommendation.RequestSnapshotJson,
            PolicySnapshotJson = recommendation.PolicySnapshotJson,
            InventorySnapshotJson = recommendation.InventorySnapshotJson,
            InventoryBatchRefsJson = SerializeAuditJson(inventoryBatchReferences),
            CandidateCountsJson = SerializeAuditJson(recommendationResult.RunTrace is null ? Array.Empty<RecommendationTraceCount>() : recommendationResult.RunTrace.CandidateCounts),
            RejectedCandidatesJson = SerializeAuditJson(recommendationResult.RunTrace is null ? Array.Empty<RecommendationRejectedCandidateTrace>() : recommendationResult.RunTrace.RejectedCandidates),
            SelectedItemsJson = SerializeAuditJson(recommendationResult.RunTrace is null ? Array.Empty<RecommendationSelectedItemTrace>() : recommendationResult.RunTrace.SelectedItems),
            FallbackFlagsJson = SerializeAuditJson(new
            {
                recommendationResult.FallbackFlags,
                selectedChannels
            }),
            BudgetUtilizationRatio = budgetUtilizationRatio,
            ManualReviewRequired = recommendationResult.ManualReviewRequired,
            FinalRationale = recommendation.Rationale,
            CreatedAt = createdAt
        };
    }

    private static IReadOnlyList<ProposalVariant> BuildProposalVariants(CampaignPlanningRequest request, Advertified.App.Data.Entities.PackageBand packageBand)
    {
        if (HasExplicitTargetMix(request))
        {
            return new[] { new ProposalVariant("requested_mix", request, null) };
        }

        var activeChannels = ResolveActiveChannels(request);
        var secondaryFocusChannel = ResolveUpperTierFocusChannel(request, activeChannels);
        var secondaryFocusLabel = secondaryFocusChannel switch
        {
            "digital" => "digital_focus",
            _ => "radio_focus"
        };
        var budgetBands = BuildProposalBudgetBands(packageBand);

        var proposals = new List<ProposalVariant>
        {
            new("balanced", ApplyChannelTargets(request, BuildBalancedTargets(request, activeChannels), budgetBands[0].PlanningBudget), budgetBands[0]),
            new("ooh_focus", ApplyChannelTargets(request, BuildFocusedTargets(request, activeChannels, "ooh"), budgetBands[1].PlanningBudget), budgetBands[1]),
            new(secondaryFocusLabel,
                ApplyChannelTargets(request, BuildFocusedTargets(request, activeChannels, secondaryFocusChannel), budgetBands[2].PlanningBudget),
                budgetBands[2])
        };

        return proposals;
    }

    private static bool HasExplicitTargetMix(CampaignPlanningRequest request)
    {
        return request.TargetRadioShare.HasValue
            || request.TargetOohShare.HasValue
            || request.TargetTvShare.HasValue
            || request.TargetDigitalShare.HasValue;
    }

    private static CampaignPlanningRequest ApplyChannelTargets(CampaignPlanningRequest source, ChannelTargets targets, decimal selectedBudget)
    {
        var clone = source.DeepClone();
        clone.SelectedBudget = selectedBudget;
        clone.TargetRadioShare = targets.Radio;
        clone.TargetOohShare = targets.Ooh;
        clone.TargetTvShare = targets.Tv;
        clone.TargetDigitalShare = targets.Digital;
        clone.BudgetAllocation = RebalanceBudgetAllocation(clone, targets);
        return clone;
    }

    private static IReadOnlyList<string> ResolveActiveChannels(CampaignPlanningRequest request)
    {
        var preferred = request.PreferredMediaTypes
            .Select(channel => channel.Trim().ToLowerInvariant())
            .Where(channel => channel is "radio" or "ooh" or "digital" or "tv" or "television")
            .Select(channel => channel is "television" ? "tv" : channel)
            .Distinct()
            .ToArray();

        return preferred.Length > 0 ? preferred : new[] { "ooh", "radio", "digital", "tv" };
    }

    private static ChannelTargets BuildBalancedTargets(CampaignPlanningRequest request, IReadOnlyList<string> activeChannels)
    {
        var allocationTargets = request.BudgetAllocation?.ChannelAllocations
            .Where(entry => entry.Weight > 0m)
            .ToDictionary(
                entry => entry.Channel,
                entry => (int)Math.Round(entry.Weight * 100m, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);
        if (allocationTargets is not null && allocationTargets.Count > 0)
        {
            return new ChannelTargets(
                allocationTargets.GetValueOrDefault("radio"),
                allocationTargets.GetValueOrDefault("ooh"),
                allocationTargets.GetValueOrDefault("tv"),
                allocationTargets.GetValueOrDefault("digital"));
        }

        var channelCount = Math.Max(1, activeChannels.Count);
        var baseAllocation = 100 / channelCount;
        var remainder = 100 - (baseAllocation * channelCount);
        var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in activeChannels)
        {
            var bump = remainder > 0 ? 1 : 0;
            targets[channel] = baseAllocation + bump;
            remainder = Math.Max(0, remainder - bump);
        }

        return new ChannelTargets(
            targets.GetValueOrDefault("radio"),
            targets.GetValueOrDefault("ooh"),
            targets.GetValueOrDefault("tv"),
            targets.GetValueOrDefault("digital"));
    }

    private static ProposalBudgetBand[] BuildProposalBudgetBands(Advertified.App.Data.Entities.PackageBand packageBand)
    {
        var minBudget = packageBand.MinBudget;
        var maxBudget = packageBand.MaxBudget;

        if (maxBudget <= minBudget)
        {
            var singleBudget = RoundCurrency(minBudget);
            return new[]
            {
                new ProposalBudgetBand(singleBudget, singleBudget, singleBudget),
                new ProposalBudgetBand(singleBudget, singleBudget, singleBudget),
                new ProposalBudgetBand(singleBudget, singleBudget, singleBudget)
            };
        }

        var span = maxBudget - minBudget;
        var firstUpper = minBudget + (span / 3m);
        var secondUpper = minBudget + ((span * 2m) / 3m);

        return new[]
        {
            CreateProposalBudgetBand(minBudget, firstUpper),
            CreateProposalBudgetBand(firstUpper, secondUpper),
            CreateProposalBudgetBand(secondUpper, maxBudget)
        };
    }

    private static ProposalBudgetBand CreateProposalBudgetBand(decimal lowerBound, decimal upperBound)
    {
        var min = RoundCurrency(lowerBound);
        var max = RoundCurrency(upperBound);
        if (max < min)
        {
            max = min;
        }

        var planningBudget = RoundCurrency((min + max) / 2m);
        if (planningBudget < min)
        {
            planningBudget = min;
        }
        else if (planningBudget > max)
        {
            planningBudget = max;
        }

        return new ProposalBudgetBand(min, max, planningBudget);
    }

    private static decimal RoundCurrency(decimal amount)
    {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static string ResolveUpperTierFocusChannel(CampaignPlanningRequest request, IReadOnlyList<string> activeChannels)
    {
        var preferred = request.PreferredMediaTypes
            .Select(channel => channel.Trim().ToLowerInvariant())
            .Where(channel => channel is "radio" or "ooh" or "digital" or "tv" or "television")
            .Select(channel => channel is "television" ? "tv" : channel)
            .Distinct()
            .ToArray();

        if (preferred.Contains("digital") && !preferred.Contains("radio"))
        {
            return "digital";
        }

        if (preferred.Contains("radio") || activeChannels.Contains("radio", StringComparer.OrdinalIgnoreCase))
        {
            return "radio";
        }

        if (activeChannels.Contains("digital", StringComparer.OrdinalIgnoreCase))
        {
            return "digital";
        }

        // Avoid creating a TV-led Proposal C because that variant can collapse into
        // a near-empty draft when TV inventory is unavailable, which breaks the
        // intended lower/mid/upper budget progression across Proposal A/B/C.
        return activeChannels.FirstOrDefault(channel => !string.Equals(channel, "ooh", StringComparison.OrdinalIgnoreCase))
            ?? "ooh";
    }

    private static ChannelTargets BuildFocusedTargets(CampaignPlanningRequest request, IReadOnlyList<string> activeChannels, string primaryChannel)
    {
        var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (activeChannels.Count == 0)
        {
            return new ChannelTargets(0, 0, 0, 0);
        }

        if (!activeChannels.Contains(primaryChannel, StringComparer.OrdinalIgnoreCase))
        {
            primaryChannel = activeChannels[0];
        }

        var balanced = BuildBalancedTargets(request, activeChannels);
        targets["radio"] = balanced.Radio;
        targets["ooh"] = balanced.Ooh;
        targets["tv"] = balanced.Tv;
        targets["digital"] = balanced.Digital;

        var primaryBoost = 15;
        targets[primaryChannel] = Math.Min(100, targets.GetValueOrDefault(primaryChannel) + primaryBoost);

        var otherChannels = activeChannels
            .Where(channel => !channel.Equals(primaryChannel, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (otherChannels.Length > 0)
        {
            var reductionPool = targets.Values.Sum() - 100;
            while (reductionPool > 0)
            {
                foreach (var channel in otherChannels)
                {
                    if (reductionPool <= 0)
                    {
                        break;
                    }

                    if (targets[channel] <= 5)
                    {
                        continue;
                    }

                    targets[channel] -= 1;
                    reductionPool -= 1;
                }
            }
        }

        return new ChannelTargets(
            targets.GetValueOrDefault("radio"),
            targets.GetValueOrDefault("ooh"),
            targets.GetValueOrDefault("tv"),
            targets.GetValueOrDefault("digital"));
    }

    private static RecommendationItem ToRecommendationItem(
        PlannedItem item,
        Guid recommendationId,
        DateTime now,
        bool isUpsell,
        RecommendationRunTrace? runTrace)
    {
        var inventoryType = isUpsell
            ? $"upsell_{item.MediaType}"
            : item.MediaType;
        var evidenceTrace = BuildEvidenceTrace(item, isUpsell, runTrace);
        var rejectionLog = BuildRejectionLog(item, runTrace);

        return new RecommendationItem
        {
            Id = Guid.NewGuid(),
            RecommendationId = recommendationId,
            InventoryType = inventoryType,
            InventoryItemId = item.SourceId,
            DisplayName = item.DisplayName,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            TotalCost = item.TotalCost,
            MetadataJson = JsonSerializer.Serialize(new
            {
                item.SourceType,
                item.MediaType,
                item.Score,
                region = BuildRegion(item),
                language = GetMetadataValue(item.Metadata, "language"),
                showDaypart = GetMetadataValue(item.Metadata, "showDaypart") ?? GetMetadataValue(item.Metadata, "show_daypart") ?? GetMetadataValue(item.Metadata, "daypart"),
                timeBand = GetMetadataValue(item.Metadata, "timeBand") ?? GetMetadataValue(item.Metadata, "time_band"),
                slotType = GetMetadataValue(item.Metadata, "slotType") ?? GetMetadataValue(item.Metadata, "slot_type"),
                duration = BuildDuration(item),
                restrictions = GetMetadataValue(item.Metadata, "restrictions") ?? GetMetadataValue(item.Metadata, "restrictionNotes"),
                confidenceScore = GetMetadataValue(item.Metadata, "confidenceScore"),
                targetAudience = GetMetadataValue(item.Metadata, "targetAudience") ?? GetMetadataValue(item.Metadata, "target_audience"),
                audienceAgeSkew = GetMetadataValue(item.Metadata, "audienceAgeSkew") ?? GetMetadataValue(item.Metadata, "audience_age_skew"),
                audienceGenderSkew = GetMetadataValue(item.Metadata, "audienceGenderSkew") ?? GetMetadataValue(item.Metadata, "audience_gender_skew"),
                audienceLsmRange = GetMetadataValue(item.Metadata, "audienceLsmRange") ?? GetMetadataValue(item.Metadata, "audience_lsm_range"),
                listenershipDaily = GetMetadataValue(item.Metadata, "listenershipDaily") ?? GetMetadataValue(item.Metadata, "listenership_daily"),
                listenershipWeekly = GetMetadataValue(item.Metadata, "listenershipWeekly") ?? GetMetadataValue(item.Metadata, "listenership_weekly"),
                listenershipPeriod = GetMetadataValue(item.Metadata, "listenershipPeriod") ?? GetMetadataValue(item.Metadata, "listenership_period"),
                selectionReasons = GetMetadataArray(item.Metadata, "selectionReasons"),
                policyFlags = GetMetadataArray(item.Metadata, "policyFlags"),
                evidenceTrace,
                evidence_trace = evidenceTrace,
                rejectionLog,
                rejection_log = rejectionLog,
                rationale = BuildRationale(item),
                item.Metadata
            }),
            CreatedAt = now
        };
    }

    private static IReadOnlyList<object> BuildEvidenceTrace(
        PlannedItem item,
        bool isUpsell,
        RecommendationRunTrace? runTrace)
    {
        var traceEntries = runTrace?.SelectedItems
            .Where(selected =>
                selected.SourceId == item.SourceId
                && string.Equals(selected.MediaType, item.MediaType, StringComparison.OrdinalIgnoreCase)
                && selected.IsUpsell == isUpsell)
            .Select(selected => new
            {
                source = "recommendation_run_trace",
                selected.Score,
                selected.SelectionReasons,
                selected.PolicyFlags,
                selected.ConfidenceScore
            })
            .Cast<object>()
            .ToList() ?? new List<object>();

        if (traceEntries.Count > 0)
        {
            return traceEntries;
        }

        var fallbackReasons = GetMetadataArray(item.Metadata, "selectionReasons");
        if (fallbackReasons.Count == 0)
        {
            fallbackReasons = new[] { "Selected by planner based on fit and budget constraints." };
        }

        return new object[]
        {
            new
            {
                source = "selection_metadata",
                Score = item.Score,
                SelectionReasons = fallbackReasons,
                PolicyFlags = GetMetadataArray(item.Metadata, "policyFlags"),
                ConfidenceScore = GetMetadataValue(item.Metadata, "confidenceScore")
            }
        };
    }

    private static IReadOnlyList<object> BuildRejectionLog(
        PlannedItem item,
        RecommendationRunTrace? runTrace)
    {
        var groupedRejections = runTrace?.RejectedCandidates
            .Where(rejected => string.Equals(rejected.MediaType, item.MediaType, StringComparison.OrdinalIgnoreCase))
            .GroupBy(rejected => new
            {
                rejected.Stage,
                rejected.Reason
            })
            .Select(group => new
            {
                group.Key.Stage,
                group.Key.Reason,
                Count = group.Count(),
                Samples = group.Select(entry => entry.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray()
            })
            .OrderByDescending(entry => entry.Count)
            .Take(5)
            .Cast<object>()
            .ToList() ?? new List<object>();

        return groupedRejections;
    }

    private static string BuildRationale(PlannedItem item)
    {
        var reasons = GetMetadataArray(item.Metadata, "selectionReasons");
        if (reasons.Count > 0)
        {
            return string.Join(" | ", reasons.Take(3));
        }

        return $"Selected with score {item.Score:n1}.";
    }

    private static string? BuildRegion(PlannedItem item)
    {
        var province = GetMetadataValue(item.Metadata, "province");
        var city = GetMetadataValue(item.Metadata, "city");
        var area = GetMetadataValue(item.Metadata, "area");
        var regionCluster = GetMetadataValue(item.Metadata, "regionClusterCode") ?? GetMetadataValue(item.Metadata, "region_cluster_code");

        return string.Join(", ", new[] { area, city, province }.Where(static part => !string.IsNullOrWhiteSpace(part)))
            switch
            {
                { Length: > 0 } value => value,
                _ => regionCluster
            };
    }

    private static string? BuildDuration(PlannedItem item)
    {
        var duration = GetMetadataValue(item.Metadata, "duration");
        if (!string.IsNullOrWhiteSpace(duration))
        {
            return duration;
        }

        var durationSeconds = GetMetadataValue(item.Metadata, "durationSeconds") ?? GetMetadataValue(item.Metadata, "duration_seconds");
        return int.TryParse(durationSeconds, out var seconds) && seconds > 0
            ? $"{seconds}s"
            : null;
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => string.IsNullOrWhiteSpace(text) ? null : text,
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => element.ToString()
            },
            _ => value.ToString()
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
            string text when !string.IsNullOrWhiteSpace(text) => new[] { text },
            JsonElement element when element.ValueKind == JsonValueKind.Array => element
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray(),
            IEnumerable<object?> items => items
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray(),
            _ => Array.Empty<string>()
        };
    }

    private static string BuildSummary(RecommendationResult result, CampaignPlanningRequest request)
    {
        var mediaMix = string.Join(", ", result.RecommendedPlan
            .Select(x => FormatSummaryChannelLabel(x.MediaType))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        var mixSummary = $"Radio {request.TargetRadioShare ?? 0}% | Billboards and Digital Screens {request.TargetOohShare ?? 0}% | TV {request.TargetTvShare ?? 0}% | Digital {request.TargetDigitalShare ?? 0}%";
        return $"Recommended {result.RecommendedPlan.Count} planned item(s) across {mediaMix}. Budget split target: {mixSummary}.";
    }

    private static string FormatSummaryChannelLabel(string? mediaType)
    {
        return (mediaType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ooh" => "Billboards and Digital Screens",
            "radio" => "Radio",
            "tv" => "TV",
            "digital" => "Digital",
            _ => mediaType ?? string.Empty
        };
    }

    private static IEnumerable<TierRecoveryRequest> BuildTierRecoveryRequests(CampaignPlanningRequest request)
    {
        if (request.MaxMediaItems.HasValue)
        {
            var relaxedMaxItems = CloneRequest(request);
            relaxedMaxItems.MaxMediaItems = null;
            yield return new TierRecoveryRequest(relaxedMaxItems, RelaxedMaxItems: true, RelaxedMix: false);
        }

        if (request.TargetRadioShare.HasValue
            || request.TargetOohShare.HasValue
            || request.TargetTvShare.HasValue
            || request.TargetDigitalShare.HasValue)
        {
            var relaxedMix = CloneRequest(request);
            relaxedMix.TargetRadioShare = null;
            relaxedMix.TargetOohShare = null;
            relaxedMix.TargetTvShare = null;
            relaxedMix.TargetDigitalShare = null;
            yield return new TierRecoveryRequest(relaxedMix, RelaxedMaxItems: false, RelaxedMix: true);

            if (request.MaxMediaItems.HasValue)
            {
                var relaxedBoth = CloneRequest(relaxedMix);
                relaxedBoth.MaxMediaItems = null;
                yield return new TierRecoveryRequest(relaxedBoth, RelaxedMaxItems: true, RelaxedMix: true);
            }
        }
    }

    private static CampaignPlanningRequest CloneRequest(CampaignPlanningRequest source)
    {
        return source.DeepClone();
    }

    private static void MarkTierRecoveryFlags(RecommendationResult recommendationResult, bool relaxedMaxItems, bool relaxedMix)
    {
        recommendationResult.ManualReviewRequired = true;
        AddFallbackFlag(recommendationResult, TierRecoveryUsedFlag);
        if (relaxedMaxItems)
        {
            AddFallbackFlag(recommendationResult, TierRecoveryRelaxedMaxItemsFlag);
        }

        if (relaxedMix)
        {
            AddFallbackFlag(recommendationResult, TierRecoveryRelaxedMixFlag);
        }
    }

    private static void AddFallbackFlag(RecommendationResult recommendationResult, string flag)
    {
        if (recommendationResult.FallbackFlags.Contains(flag, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        recommendationResult.FallbackFlags.Add(flag);
    }

    private static string BuildStoredRationale(RecommendationResult result, string? visibleRationaleOverride)
    {
        var visibleRationale = string.IsNullOrWhiteSpace(visibleRationaleOverride)
            ? result.Rationale.Trim()
            : visibleRationaleOverride.Trim();

        var sections = new List<string> { visibleRationale };
        sections.Add($"{ManualReviewMarker} {result.ManualReviewRequired}");

        if (result.FallbackFlags.Count > 0)
        {
            sections.Add($"{FallbackFlagsMarker} {string.Join(", ", result.FallbackFlags)}");
        }

        return string.Join(Environment.NewLine, sections.Where(section => !string.IsNullOrWhiteSpace(section)));
    }

    private static void EnsureRecommendationFallsWithinTier(ProposalVariant variant, RecommendationResult recommendationResult)
    {
        if (variant.BudgetBand is null)
        {
            return;
        }

        var total = recommendationResult.RecommendedPlanTotal;
        if (IsWithinProposalTier(total, variant.BudgetBand.MinBudget, variant.BudgetBand.MaxBudget))
        {
            return;
        }

        if (IsWithinProposalTierTolerance(total, variant.BudgetBand.MinBudget, variant.BudgetBand.MaxBudget))
        {
            if (!recommendationResult.FallbackFlags.Contains(TierBoundaryToleranceFlag, StringComparer.OrdinalIgnoreCase))
            {
                recommendationResult.FallbackFlags.Add(TierBoundaryToleranceFlag);
            }

            recommendationResult.ManualReviewRequired = true;
            return;
        }

        throw new InvalidOperationException(
            $"Could not generate {GetProposalDisplayLabel(variant.Key)} within its required tier of {FormatCurrency(variant.BudgetBand.MinBudget)} to {FormatCurrency(variant.BudgetBand.MaxBudget)}. " +
            $"The generated total was {FormatCurrency(total)}.");
    }

    internal static bool IsWithinProposalTier(decimal total, decimal minBudget, decimal maxBudget)
    {
        return total >= minBudget && total <= maxBudget;
    }

    internal static bool IsWithinProposalTierTolerance(decimal total, decimal minBudget, decimal maxBudget)
    {
        if (IsWithinProposalTier(total, minBudget, maxBudget))
        {
            return true;
        }

        var tolerance = GetProposalTierTolerance(minBudget, maxBudget);
        if (tolerance <= 0m)
        {
            return false;
        }

        return total >= minBudget - tolerance - ProposalTierRoundingSlack
            && total <= maxBudget + tolerance + ProposalTierRoundingSlack;
    }

    internal static decimal GetProposalTierTolerance(decimal minBudget, decimal maxBudget)
    {
        if (maxBudget <= minBudget || maxBudget <= 0m)
        {
            return 0m;
        }

        var span = maxBudget - minBudget;
        return RoundCurrency(Math.Min(
            maxBudget * ProposalTierMaxBudgetToleranceRatio,
            span * ProposalTierSpanToleranceRatio));
    }

    private static string GetProposalDisplayLabel(string variantKey)
    {
        return variantKey.ToLowerInvariant() switch
        {
            "balanced" => "Proposal A",
            "ooh_focus" => "Proposal B",
            "radio_focus" => "Proposal C",
            "digital_focus" => "Proposal C",
            _ => "the proposal"
        };
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }

    private async Task<InventorySnapshot> BuildInventorySnapshotAsync(CancellationToken cancellationToken)
    {
        var activeBatches = await _db.InventoryImportBatches
            .AsNoTracking()
            .Where(batch => batch.IsActive)
            .OrderBy(batch => batch.ChannelFamily)
            .Select(batch => new InventoryBatchSnapshot(
                batch.Id,
                batch.ChannelFamily,
                batch.SourceType,
                batch.SourceIdentifier,
                batch.SourceChecksum,
                batch.RecordCount,
                batch.Status,
                batch.CreatedAt,
                batch.ActivatedAt))
            .ToListAsync(cancellationToken);

        var batchReferences = activeBatches
            .Select(batch => new InventoryBatchReferenceSnapshot(
                batch.ChannelFamily,
                batch.Id,
                batch.SourceIdentifier,
                batch.ActivatedAt ?? batch.CreatedAt))
            .ToArray();

        return new InventorySnapshot(
            activeBatches,
            batchReferences);
    }

    private sealed record InventorySnapshot(
        IReadOnlyList<InventoryBatchSnapshot> ActiveBatches,
        IReadOnlyList<InventoryBatchReferenceSnapshot> BatchReferences);

    private sealed record InventoryBatchSnapshot(
        Guid Id,
        string ChannelFamily,
        string SourceType,
        string SourceIdentifier,
        string? SourceChecksum,
        int RecordCount,
        string Status,
        DateTime CreatedAt,
        DateTime? ActivatedAt);

    private sealed record InventoryBatchReferenceSnapshot(
        string ChannelFamily,
        Guid BatchId,
        string SourceIdentifier,
        DateTime ActiveAt);

    private sealed record TierRecoveryRequest(
        CampaignPlanningRequest Request,
        bool RelaxedMaxItems,
        bool RelaxedMix);

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
            BusinessStage = request.BusinessStage,
            MonthlyRevenueBand = request.MonthlyRevenueBand,
            SalesModel = request.SalesModel,
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
            TargetLatitude = request.TargetLatitude,
            TargetLongitude = request.TargetLongitude
        };
    }

    private static PlanningBudgetAllocation? RebalanceBudgetAllocation(CampaignPlanningRequest request, ChannelTargets targets)
    {
        if (request.BudgetAllocation is null)
        {
            return null;
        }

        var channelWeights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["radio"] = targets.Radio / 100m,
            ["ooh"] = targets.Ooh / 100m,
            ["tv"] = targets.Tv / 100m,
            ["digital"] = targets.Digital / 100m
        }
        .Where(entry => entry.Value > 0m)
        .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        var totalWeight = channelWeights.Sum(entry => entry.Value);
        if (totalWeight <= 0m)
        {
            totalWeight = 1m;
        }

        var normalizedChannels = channelWeights.ToDictionary(
            entry => entry.Key,
            entry => decimal.Round(entry.Value / totalWeight, 4, MidpointRounding.AwayFromZero),
            StringComparer.OrdinalIgnoreCase);

        var geoWeights = request.BudgetAllocation.GeoAllocations
            .ToDictionary(entry => entry.Bucket, entry => entry.Weight, StringComparer.OrdinalIgnoreCase);

        var allocation = new PlanningBudgetAllocation
        {
            AudienceSegment = request.BudgetAllocation.AudienceSegment,
            ChannelPolicyKey = request.BudgetAllocation.ChannelPolicyKey,
            GeoPolicyKey = request.BudgetAllocation.GeoPolicyKey,
            ChannelAllocations = normalizedChannels
                .Select(entry => new PlanningChannelAllocation
                {
                    Channel = entry.Key,
                    Weight = entry.Value,
                    Amount = decimal.Round(request.SelectedBudget * entry.Value, 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(entry => entry.Weight)
                .ToList(),
            GeoAllocations = request.BudgetAllocation.GeoAllocations
                .Select(entry => entry.DeepClone())
                .ToList()
        };

        foreach (var geoAllocation in allocation.GeoAllocations)
        {
            geoAllocation.Amount = decimal.Round(request.SelectedBudget * geoAllocation.Weight, 2, MidpointRounding.AwayFromZero);
        }

        foreach (var channel in allocation.ChannelAllocations)
        {
            foreach (var geo in allocation.GeoAllocations)
            {
                allocation.CompositeAllocations.Add(new PlanningAllocationLine
                {
                    Channel = channel.Channel,
                    Bucket = geo.Bucket,
                    Weight = decimal.Round(channel.Weight * geo.Weight, 4, MidpointRounding.AwayFromZero),
                    Amount = decimal.Round(request.SelectedBudget * channel.Weight * geo.Weight, 2, MidpointRounding.AwayFromZero),
                    RadiusKm = geo.RadiusKm
                });
            }
        }

        return allocation;
    }

    private static string SerializeAuditJson(object value)
    {
        return JsonSerializer.Serialize(value, AuditJsonOptions);
    }

    private sealed class NullPlanningTargetResolver : ICampaignPlanningTargetResolver
    {
        public CampaignPlanningTargetResolution Resolve(CampaignBriefEntity? brief)
        {
            return new CampaignPlanningTargetResolution();
        }

        public CampaignPlanningTargetResolution Resolve(CampaignPlanningRequest request)
        {
            return new CampaignPlanningTargetResolution
            {
                Label = request.TargetLocationLabel ?? string.Empty,
                City = request.TargetLocationCity,
                Province = request.TargetLocationProvince,
                Latitude = request.TargetLatitude,
                Longitude = request.TargetLongitude,
                Source = "none",
                Precision = request.TargetLocationPrecision ?? "unknown",
                IsResolved = request.TargetLatitude.HasValue && request.TargetLongitude.HasValue
            };
        }
    }

    private sealed class NullBusinessLocationResolver : ICampaignBusinessLocationResolver
    {
        public CampaignBusinessLocationResolution Resolve(CampaignEntity campaign)
        {
            return new CampaignBusinessLocationResolution();
        }
    }

    private sealed class NullPlanningBudgetAllocationService : IPlanningBudgetAllocationService
    {
        public PlanningBudgetAllocation Resolve(CampaignPlanningRequest request)
        {
            return new PlanningBudgetAllocation();
        }

        public PlanningBudgetAllocation RebalanceChannelTargets(CampaignPlanningRequest request, IReadOnlyDictionary<string, int> channelShares)
        {
            return new PlanningBudgetAllocation();
        }
    }

    private sealed record ProposalVariant(string Key, CampaignPlanningRequest Request, ProposalBudgetBand? BudgetBand);
    private sealed record ProposalBudgetBand(decimal MinBudget, decimal MaxBudget, decimal PlanningBudget);
    private sealed record ChannelTargets(int Radio, int Ooh, int Tv, int Digital);
}


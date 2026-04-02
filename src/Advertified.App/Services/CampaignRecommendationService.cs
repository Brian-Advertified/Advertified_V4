using System.Text.Json;
using Advertified.App.Campaigns;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services;

public sealed class CampaignRecommendationService : ICampaignRecommendationService
{
    private const string FallbackFlagsMarker = "Fallback flags:";
    private const string ManualReviewMarker = "Manual review required:";
    private readonly AppDbContext _db;
    private readonly IMediaPlanningEngine _planningEngine;
    private readonly ICampaignReasoningService _campaignReasoningService;

    public CampaignRecommendationService(
        AppDbContext db,
        IMediaPlanningEngine planningEngine,
        ICampaignReasoningService campaignReasoningService)
    {
        _db = db;
        _planningEngine = planningEngine;
        _campaignReasoningService = campaignReasoningService;
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
        Guid? primaryRecommendationId = null;
        var revisionNumber = RecommendationRevisionSupport.GetNextRevisionNumber(campaign.CampaignRecommendations);

        for (var index = 0; index < proposalVariants.Count; index++)
        {
            var variant = proposalVariants[index];
            var recommendationResult = await _planningEngine.GenerateAsync(variant.Request, cancellationToken);
            RecommendationOohPolicy.EnsureGeneratedPlanContainsOoh(recommendationResult.RecommendedPlan);

            var aiReasoning = await _campaignReasoningService.GenerateAsync(campaign, brief, variant.Request, recommendationResult, cancellationToken);
            var proposalTimestamp = now.AddMilliseconds(index);
            var recommendation = CreateRecommendationEntity(campaignId, campaign.PlanningMode, variant.Key, recommendationResult, variant.Request, aiReasoning, proposalTimestamp, revisionNumber);

            foreach (var item in recommendationResult.RecommendedPlan)
            {
                recommendation.RecommendationItems.Add(ToRecommendationItem(item, recommendation.Id, proposalTimestamp, isUpsell: false));
            }

            foreach (var item in recommendationResult.Upsells)
            {
                recommendation.RecommendationItems.Add(ToRecommendationItem(item, recommendation.Id, proposalTimestamp, isUpsell: true));
            }

            primaryRecommendationId ??= recommendation.Id;
            _db.CampaignRecommendations.Add(recommendation);
        }

        campaign.Status = "planning_in_progress";
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return primaryRecommendationId ?? Guid.Empty;
    }

    private static CampaignPlanningRequest BuildRequest(
        CampaignEntity campaign,
        CampaignBriefEntity brief,
        GenerateRecommendationRequest? request,
        PackageBandProfile? packageProfile)
    {
        var preferredMediaTypes = brief.GetList(nameof(CampaignBriefEntity.PreferredMediaTypesJson)).ToList();
        if (string.Equals(packageProfile?.IncludeTv, "yes", StringComparison.OrdinalIgnoreCase)
            && !preferredMediaTypes.Any(media =>
                string.Equals(media?.Trim(), "tv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(media?.Trim(), "television", StringComparison.OrdinalIgnoreCase)))
        {
            preferredMediaTypes.Add("tv");
        }

        var normalizedScope = NormalizeGeographyScope(brief.GeographyScope);
        var provinces = normalizedScope == "provincial"
            ? brief.GetList(nameof(CampaignBriefEntity.ProvincesJson))
            : new List<string>();
        var cities = normalizedScope == "local"
            ? brief.GetList(nameof(CampaignBriefEntity.CitiesJson))
            : new List<string>();
        var suburbs = normalizedScope == "local"
            ? brief.GetList(nameof(CampaignBriefEntity.SuburbsJson))
            : new List<string>();
        var areas = normalizedScope == "local"
            ? brief.GetList(nameof(CampaignBriefEntity.AreasJson))
            : new List<string>();

        return new CampaignPlanningRequest
        {
            CampaignId = campaign.Id,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
            GeographyScope = normalizedScope,
            Provinces = provinces,
            Cities = cities,
            Suburbs = suburbs,
            Areas = areas,
            PreferredMediaTypes = preferredMediaTypes,
            ExcludedMediaTypes = brief.GetList(nameof(CampaignBriefEntity.ExcludedMediaTypesJson)),
            TargetLanguages = brief.GetList(nameof(CampaignBriefEntity.TargetLanguagesJson)),
            TargetLsmMin = brief.TargetLsmMin,
            TargetLsmMax = brief.TargetLsmMax,
            OpenToUpsell = brief.OpenToUpsell,
            AdditionalBudget = brief.AdditionalBudget,
            MaxMediaItems = brief.MaxMediaItems,
            TargetRadioShare = request?.TargetRadioShare,
            TargetOohShare = request?.TargetOohShare,
            TargetTvShare = request?.TargetTvShare,
            TargetDigitalShare = request?.TargetDigitalShare
        };
    }

    private static string NormalizeGeographyScope(string? scope)
    {
        return (scope ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "regional" => "provincial",
            "local" => "local",
            "provincial" => "provincial",
            "national" => "national",
            _ => "provincial"
        };
    }

    private static CampaignRecommendation CreateRecommendationEntity(
        Guid campaignId,
        string? planningMode,
        string variantKey,
        RecommendationResult recommendationResult,
        CampaignPlanningRequest planningRequest,
        CampaignReasoningResult? aiReasoning,
        DateTime now,
        int revisionNumber)
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
            RevisionNumber = revisionNumber,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<ProposalVariant> BuildProposalVariants(CampaignPlanningRequest request, Advertified.App.Data.Entities.PackageBand packageBand)
    {
        if (HasExplicitTargetMix(request))
        {
            return new[] { new ProposalVariant("requested_mix", request) };
        }

        var activeChannels = ResolveActiveChannels(request);
        var secondaryFocusChannel = activeChannels.Contains("digital", StringComparer.OrdinalIgnoreCase)
            ? "digital"
            : activeChannels.Contains("tv", StringComparer.OrdinalIgnoreCase)
                ? "tv"
                : "radio";
        var secondaryFocusLabel = secondaryFocusChannel switch
        {
            "digital" => "digital_focus",
            "tv" => "tv_focus",
            _ => "radio_focus"
        };
        var budgetBands = BuildProposalBudgetBands(packageBand);

        var proposals = new List<ProposalVariant>
        {
            new("balanced", ApplyChannelTargets(request, BuildBalancedTargets(activeChannels), budgetBands[0].PlanningBudget)),
            new("ooh_focus", ApplyChannelTargets(request, BuildFocusedTargets(activeChannels, "ooh"), budgetBands[1].PlanningBudget)),
            new(secondaryFocusLabel,
                ApplyChannelTargets(request, BuildFocusedTargets(activeChannels, secondaryFocusChannel), budgetBands[2].PlanningBudget))
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
        return new CampaignPlanningRequest
        {
            CampaignId = source.CampaignId,
            SelectedBudget = selectedBudget,
            GeographyScope = source.GeographyScope,
            Provinces = source.Provinces.ToList(),
            Cities = source.Cities.ToList(),
            Suburbs = source.Suburbs.ToList(),
            Areas = source.Areas.ToList(),
            PreferredMediaTypes = source.PreferredMediaTypes.ToList(),
            ExcludedMediaTypes = source.ExcludedMediaTypes.ToList(),
            TargetLanguages = source.TargetLanguages.ToList(),
            TargetLsmMin = source.TargetLsmMin,
            TargetLsmMax = source.TargetLsmMax,
            OpenToUpsell = source.OpenToUpsell,
            AdditionalBudget = source.AdditionalBudget,
            MaxMediaItems = source.MaxMediaItems,
            TargetRadioShare = targets.Radio,
            TargetOohShare = targets.Ooh,
            TargetTvShare = targets.Tv,
            TargetDigitalShare = targets.Digital
        };
    }

    private static IReadOnlyList<string> ResolveActiveChannels(CampaignPlanningRequest request)
    {
        var preferred = request.PreferredMediaTypes
            .Select(channel => channel.Trim().ToLowerInvariant())
            .Where(channel => channel is "radio" or "ooh" or "digital" or "tv" or "television")
            .Select(channel => channel is "television" ? "tv" : channel)
            .Distinct()
            .ToArray();

        return preferred.Length > 0 ? preferred : new[] { "ooh", "radio", "tv" };
    }

    private static ChannelTargets BuildBalancedTargets(IReadOnlyList<string> activeChannels)
    {
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

    private static ChannelTargets BuildFocusedTargets(IReadOnlyList<string> activeChannels, string primaryChannel)
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

        var remainderChannels = activeChannels
            .Where(channel => !channel.Equals(primaryChannel, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        targets[primaryChannel] = 60;

        if (remainderChannels.Length == 0)
        {
            targets[primaryChannel] = 100;
        }
        else
        {
            var baseAllocation = 40 / remainderChannels.Length;
            var remainder = 40 - (baseAllocation * remainderChannels.Length);
            foreach (var channel in remainderChannels)
            {
                var bump = remainder > 0 ? 1 : 0;
                targets[channel] = baseAllocation + bump;
                remainder = Math.Max(0, remainder - bump);
            }
        }

        return new ChannelTargets(
            targets.GetValueOrDefault("radio"),
            targets.GetValueOrDefault("ooh"),
            targets.GetValueOrDefault("tv"),
            targets.GetValueOrDefault("digital"));
    }

    private static RecommendationItem ToRecommendationItem(PlannedItem item, Guid recommendationId, DateTime now, bool isUpsell)
    {
        var inventoryType = isUpsell
            ? $"upsell_{item.MediaType}"
            : item.MediaType;

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
                selectionReasons = GetMetadataArray(item.Metadata, "selectionReasons"),
                policyFlags = GetMetadataArray(item.Metadata, "policyFlags"),
                rationale = BuildRationale(item),
                item.Metadata
            }),
            CreatedAt = now
        };
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
        var mediaMix = string.Join(", ", result.RecommendedPlan.Select(x => x.MediaType).Distinct());
        var mixSummary = $"Radio {request.TargetRadioShare ?? 0}% | Billboards and Digital Screens {request.TargetOohShare ?? 0}% | TV {request.TargetTvShare ?? 0}% | Digital {request.TargetDigitalShare ?? 0}%";
        return $"Recommended {result.RecommendedPlan.Count} planned item(s) across {mediaMix}. Budget split target: {mixSummary}.";
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

    private sealed record ProposalVariant(string Key, CampaignPlanningRequest Request);
    private sealed record ProposalBudgetBand(decimal MinBudget, decimal MaxBudget, decimal PlanningBudget);
    private sealed record ChannelTargets(int Radio, int Ooh, int Tv, int Digital);
}


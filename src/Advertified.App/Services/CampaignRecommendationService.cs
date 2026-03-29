using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
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

    public async Task<Guid> GenerateAndSaveAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var brief = campaign.CampaignBrief
            ?? throw new InvalidOperationException("Campaign brief not found.");

        var request = BuildRequest(campaign, brief);
        var recommendationResult = await _planningEngine.GenerateAsync(request, cancellationToken);
        var aiReasoning = await _campaignReasoningService.GenerateAsync(campaign, brief, request, recommendationResult, cancellationToken);

        var now = DateTime.UtcNow;
        var recommendation = new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RecommendationType = campaign.PlanningMode ?? "ai_assisted",
            GeneratedBy = "system",
            Status = "draft",
            TotalCost = recommendationResult.RecommendedPlanTotal,
            Summary = aiReasoning?.Summary ?? BuildSummary(recommendationResult),
            Rationale = BuildStoredRationale(recommendationResult, aiReasoning?.Rationale),
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var item in recommendationResult.RecommendedPlan)
        {
            recommendation.RecommendationItems.Add(ToRecommendationItem(item, recommendation.Id, now, isUpsell: false));
        }

        foreach (var item in recommendationResult.Upsells)
        {
            recommendation.RecommendationItems.Add(ToRecommendationItem(item, recommendation.Id, now, isUpsell: true));
        }

        _db.CampaignRecommendations.Add(recommendation);
        campaign.Status = "planning_in_progress";
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return recommendation.Id;
    }

    private static CampaignPlanningRequest BuildRequest(CampaignEntity campaign, CampaignBriefEntity brief)
    {
        return new CampaignPlanningRequest
        {
            CampaignId = campaign.Id,
            SelectedBudget = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            GeographyScope = brief.GeographyScope,
            Provinces = brief.GetList(nameof(CampaignBriefEntity.ProvincesJson)),
            Cities = brief.GetList(nameof(CampaignBriefEntity.CitiesJson)),
            Suburbs = brief.GetList(nameof(CampaignBriefEntity.SuburbsJson)),
            Areas = brief.GetList(nameof(CampaignBriefEntity.AreasJson)),
            PreferredMediaTypes = brief.GetList(nameof(CampaignBriefEntity.PreferredMediaTypesJson)),
            ExcludedMediaTypes = brief.GetList(nameof(CampaignBriefEntity.ExcludedMediaTypesJson)),
            TargetLanguages = brief.GetList(nameof(CampaignBriefEntity.TargetLanguagesJson)),
            TargetLsmMin = brief.TargetLsmMin,
            TargetLsmMax = brief.TargetLsmMax,
            OpenToUpsell = brief.OpenToUpsell,
            AdditionalBudget = brief.AdditionalBudget,
            MaxMediaItems = brief.MaxMediaItems
        };
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

    private static string BuildSummary(RecommendationResult result)
    {
        var mediaMix = string.Join(", ", result.RecommendedPlan.Select(x => x.MediaType).Distinct());
        return $"Recommended {result.RecommendedPlan.Count} planned item(s) across {mediaMix}.";
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
}

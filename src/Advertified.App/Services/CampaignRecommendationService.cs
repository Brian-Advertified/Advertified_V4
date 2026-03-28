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
    private readonly AppDbContext _db;
    private readonly IMediaPlanningEngine _planningEngine;

    public CampaignRecommendationService(AppDbContext db, IMediaPlanningEngine planningEngine)
    {
        _db = db;
        _planningEngine = planningEngine;
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

        var now = DateTime.UtcNow;
        var recommendation = new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            RecommendationType = campaign.PlanningMode ?? "ai_assisted",
            GeneratedBy = "system",
            Status = "draft",
            TotalCost = recommendationResult.RecommendedPlanTotal,
            Summary = BuildSummary(recommendationResult),
            Rationale = recommendationResult.Rationale,
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
                rationale = $"Selected with score {item.Score:n1}.",
                item.Metadata
            }),
            CreatedAt = now
        };
    }

    private static string BuildSummary(RecommendationResult result)
    {
        var mediaMix = string.Join(", ", result.RecommendedPlan.Select(x => x.MediaType).Distinct());
        return $"Recommended {result.RecommendedPlan.Count} planned item(s) across {mediaMix}.";
    }
}

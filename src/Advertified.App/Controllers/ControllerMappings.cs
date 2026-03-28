using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Packages;
using Advertified.App.Data.Entities;

namespace Advertified.App.Controllers;

internal static class ControllerMappings
{
    private const string ClientFeedbackMarker = "Client feedback:";

    public static CampaignListItemResponse ToListItem(this Campaign campaign, Guid? currentUserId = null)
    {
        return new CampaignListItemResponse
        {
            Id = campaign.Id,
            PackageOrderId = campaign.PackageOrderId,
            PackageBandId = campaign.PackageBandId,
            PackageBandName = campaign.PackageBand.Name,
            SelectedBudget = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            CampaignName = campaign.CampaignName,
            Status = campaign.Status,
            PlanningMode = campaign.PlanningMode,
            AiUnlocked = campaign.AiUnlocked,
            AgentAssistanceRequested = campaign.AgentAssistanceRequested,
            AssignedAgentUserId = campaign.AssignedAgentUserId,
            AssignedAgentName = campaign.AssignedAgentUser?.FullName,
            AssignedAt = campaign.AssignedAt.HasValue ? new DateTimeOffset(campaign.AssignedAt.Value, TimeSpan.Zero) : null,
            IsAssignedToCurrentUser = currentUserId.HasValue && campaign.AssignedAgentUserId == currentUserId.Value,
            IsUnassigned = campaign.AssignedAgentUserId is null,
            NextAction = GetNextAction(campaign),
            CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero)
        };
    }

    public static CampaignDetailResponse ToDetail(this Campaign campaign, Guid? currentUserId = null)
    {
        return new CampaignDetailResponse
        {
            Id = campaign.Id,
            UserId = campaign.UserId,
            ClientName = campaign.User.FullName,
            ClientEmail = campaign.User.Email,
            BusinessName = campaign.User.BusinessProfile?.BusinessName,
            Industry = campaign.User.BusinessProfile?.Industry,
            PackageOrderId = campaign.PackageOrderId,
            PackageBandId = campaign.PackageBandId,
            PackageBandName = campaign.PackageBand.Name,
            SelectedBudget = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            CampaignName = campaign.CampaignName,
            Status = campaign.Status,
            PlanningMode = campaign.PlanningMode,
            AiUnlocked = campaign.AiUnlocked,
            AgentAssistanceRequested = campaign.AgentAssistanceRequested,
            AssignedAgentUserId = campaign.AssignedAgentUserId,
            AssignedAgentName = campaign.AssignedAgentUser?.FullName,
            AssignedAt = campaign.AssignedAt.HasValue ? new DateTimeOffset(campaign.AssignedAt.Value, TimeSpan.Zero) : null,
            IsAssignedToCurrentUser = currentUserId.HasValue && campaign.AssignedAgentUserId == currentUserId.Value,
            IsUnassigned = campaign.AssignedAgentUserId is null,
            NextAction = GetNextAction(campaign),
            CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero),
            Brief = campaign.CampaignBrief == null ? null : ToRequest(campaign.CampaignBrief),
            Recommendation = campaign.CampaignRecommendations
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToResponse)
                .FirstOrDefault()
        };
    }

    public static PackageOrderListItemResponse ToListItem(this PackageOrder order)
    {
        return new PackageOrderListItemResponse
        {
            Id = order.Id,
            UserId = order.UserId,
            PackageBandId = order.PackageBandId,
            PackageBandName = order.PackageBand.Name,
            Amount = order.SelectedBudget ?? order.Amount,
            Currency = order.Currency,
            PaymentProvider = order.PaymentProvider ?? string.Empty,
            PaymentStatus = order.PaymentStatus,
            PaymentReference = order.PaymentReference,
            CreatedAt = new DateTimeOffset(order.CreatedAt, TimeSpan.Zero),
            InvoiceId = order.Invoice?.Id,
            InvoiceStatus = order.Invoice?.Status,
            InvoicePdfUrl = order.Invoice is null ? null : $"/invoices/{order.Invoice.Id}/pdf"
        };
    }

    private static SaveCampaignBriefRequest ToRequest(CampaignBrief brief)
    {
        return new SaveCampaignBriefRequest
        {
            Objective = brief.Objective,
            StartDate = brief.StartDate,
            EndDate = brief.EndDate,
            DurationWeeks = brief.DurationWeeks,
            GeographyScope = brief.GeographyScope,
            Provinces = DeserializeList(brief.ProvincesJson),
            Cities = DeserializeList(brief.CitiesJson),
            Suburbs = DeserializeList(brief.SuburbsJson),
            Areas = DeserializeList(brief.AreasJson),
            TargetAgeMin = brief.TargetAgeMin,
            TargetAgeMax = brief.TargetAgeMax,
            TargetGender = brief.TargetGender,
            TargetLanguages = DeserializeList(brief.TargetLanguagesJson),
            TargetLsmMin = brief.TargetLsmMin,
            TargetLsmMax = brief.TargetLsmMax,
            TargetInterests = DeserializeList(brief.TargetInterestsJson),
            TargetAudienceNotes = brief.TargetAudienceNotes,
            PreferredMediaTypes = DeserializeList(brief.PreferredMediaTypesJson),
            ExcludedMediaTypes = DeserializeList(brief.ExcludedMediaTypesJson),
            MustHaveAreas = DeserializeList(brief.MustHaveAreasJson),
            ExcludedAreas = DeserializeList(brief.ExcludedAreasJson),
            CreativeReady = brief.CreativeReady,
            CreativeNotes = brief.CreativeNotes,
            MaxMediaItems = brief.MaxMediaItems,
            OpenToUpsell = brief.OpenToUpsell,
            AdditionalBudget = brief.AdditionalBudget,
            SpecialRequirements = brief.SpecialRequirements
        };
    }

    private static List<string>? DeserializeList(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<List<string>>(json);
    }

    private static CampaignRecommendationResponse ToResponse(CampaignRecommendation recommendation)
    {
        var extractedFeedback = ExtractClientFeedbackNotes(recommendation.Rationale);

        return new CampaignRecommendationResponse
        {
            Id = recommendation.Id,
            CampaignId = recommendation.CampaignId,
            Summary = recommendation.Summary ?? string.Empty,
            Rationale = RemoveClientFeedbackNotes(recommendation.Rationale),
            ClientFeedbackNotes = extractedFeedback,
            Status = recommendation.Status,
            TotalCost = recommendation.TotalCost,
            Items = recommendation.RecommendationItems.Select(item => new RecommendationItemResponse
            {
                Id = item.Id,
                SourceInventoryId = ExtractMetadataValue(item.MetadataJson, "sourceInventoryId"),
                Quantity = item.Quantity,
                Flighting = ExtractMetadataValue(item.MetadataJson, "flighting"),
                ItemNotes = ExtractMetadataValue(item.MetadataJson, "itemNotes"),
                StartDate = ExtractMetadataValue(item.MetadataJson, "startDate"),
                EndDate = ExtractMetadataValue(item.MetadataJson, "endDate"),
                Title = item.DisplayName,
                Channel = item.InventoryType,
                Rationale = ExtractRationale(item.MetadataJson),
                Cost = item.TotalCost,
                Type = item.InventoryType.Contains("upsell", StringComparison.OrdinalIgnoreCase) ? "upsell" : "base"
            }).ToArray()
        };
    }

    private static string GetNextAction(Campaign campaign)
    {
        return campaign.Status switch
        {
            "paid" => "Complete your campaign brief",
            "brief_in_progress" => "Finish and submit your brief",
            "brief_submitted" => "Choose planning mode",
            "planning_in_progress" => "Review your tailored recommendation",
            "review_ready" => "Approve or request updates",
            "approved" => "Recommendation approved",
            _ => "Continue campaign setup"
        };
    }

    private static string ExtractRationale(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("rationale", out var rationale))
            {
                return rationale.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static string? ExtractMetadataValue(string? metadataJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(propertyName, out var value))
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string RemoveClientFeedbackNotes(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return string.Empty;
        }

        var markerIndex = rationale.LastIndexOf(ClientFeedbackMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return rationale.Trim();
        }

        return rationale[..markerIndex].TrimEnd();
    }

    private static string? ExtractClientFeedbackNotes(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        var markerIndex = rationale.LastIndexOf(ClientFeedbackMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var notes = rationale[(markerIndex + ClientFeedbackMarker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
    }
}

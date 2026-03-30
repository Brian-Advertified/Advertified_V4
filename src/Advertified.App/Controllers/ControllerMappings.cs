using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Creative;
using Advertified.App.Contracts.Packages;
using Advertified.App.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Support;

namespace Advertified.App.Controllers;

internal static class ControllerMappings
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private const string FallbackFlagsMarker = "Fallback flags:";
    private const string ManualReviewMarker = "Manual review required:";

    public static CampaignListItemResponse ToListItem(this Campaign campaign, Guid? currentUserId = null)
    {
        return new CampaignListItemResponse
        {
            Id = campaign.Id,
            PackageOrderId = campaign.PackageOrderId,
            PackageBandId = campaign.PackageBandId,
            PackageBandName = campaign.PackageBand.Name,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
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
            NextAction = CampaignWorkflowPolicy.GetClientNextAction(campaign),
            CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero)
        };
    }

    public static CampaignDetailResponse ToDetail(this Campaign campaign, Guid? currentUserId = null, bool includeLinePricing = true)
    {
        var recommendations = GetCurrentRecommendationSet(campaign)
            .Select(x => ToResponse(x, includeLinePricing))
            .ToArray();
        var creativeSystems = campaign.CampaignCreativeSystems
            .OrderByDescending(x => x.CreatedAt)
            .Select(ToResponse)
            .ToArray();
        var schedule = CampaignOperationsPolicy.BuildScheduleSnapshot(campaign, DateOnly.FromDateTime(DateTime.UtcNow));

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
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
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
            NextAction = CampaignWorkflowPolicy.GetClientNextAction(campaign),
            CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero),
            Timeline = CampaignWorkflowPolicy.BuildTimeline(campaign),
            Brief = campaign.CampaignBrief == null ? null : ToRequest(campaign.CampaignBrief),
            Recommendations = recommendations,
            Recommendation = recommendations.FirstOrDefault(),
            RecommendationPdfUrl = recommendations.Length > 0 ? $"/campaigns/{campaign.Id}/recommendation-pdf" : null,
            CreativeSystems = creativeSystems,
            LatestCreativeSystem = creativeSystems.FirstOrDefault(),
            Assets = campaign.CampaignAssets.OrderByDescending(x => x.CreatedAt).Select(ToResponse).ToArray(),
            SupplierBookings = campaign.CampaignSupplierBookings
                .OrderBy(x => x.LiveFrom ?? DateOnly.MaxValue)
                .ThenBy(x => x.SupplierOrStation)
                .Select(ToResponse)
                .ToArray(),
            DeliveryReports = campaign.CampaignDeliveryReports
                .OrderByDescending(x => x.ReportedAt ?? x.CreatedAt)
                .Select(ToResponse)
                .ToArray(),
            EffectiveEndDate = schedule.EffectiveEndDate,
            DaysLeft = schedule.DaysLeft
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
            Amount = order.Amount,
            Currency = order.Currency,
            PaymentProvider = order.PaymentProvider ?? string.Empty,
            PaymentStatus = order.PaymentStatus,
            RefundStatus = order.RefundStatus,
            RefundedAmount = order.RefundedAmount,
            GatewayFeeRetainedAmount = order.GatewayFeeRetainedAmount,
            RefundReason = order.RefundReason,
            RefundProcessedAt = order.RefundProcessedAt.HasValue ? new DateTimeOffset(order.RefundProcessedAt.Value, TimeSpan.Zero) : null,
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

    private static CampaignRecommendationResponse ToResponse(CampaignRecommendation recommendation, bool includeLinePricing)
    {
        var extractedFeedback = ExtractClientFeedbackNotes(recommendation.Rationale);
        var fallbackFlags = ExtractFallbackFlags(recommendation.Rationale);
        var manualReviewRequired = ExtractManualReviewRequired(recommendation.Rationale);
        var (proposalLabel, proposalStrategy) = GetProposalDetails(recommendation.RecommendationType);

        return new CampaignRecommendationResponse
        {
            Id = recommendation.Id,
            CampaignId = recommendation.CampaignId,
            ProposalLabel = proposalLabel,
            ProposalStrategy = proposalStrategy,
            Summary = recommendation.Summary ?? string.Empty,
            Rationale = RemoveInternalMarkers(recommendation.Rationale),
            ClientFeedbackNotes = extractedFeedback,
            ManualReviewRequired = manualReviewRequired,
            FallbackFlags = fallbackFlags,
            Status = recommendation.Status,
            TotalCost = recommendation.TotalCost,
            Items = recommendation.RecommendationItems.Select(item => ToResponse(item, includeLinePricing)).ToArray()
        };
    }

    private static CampaignAssetResponse ToResponse(CampaignAsset asset)
    {
        return new CampaignAssetResponse
        {
            Id = asset.Id,
            AssetType = asset.AssetType,
            DisplayName = asset.DisplayName,
            PublicUrl = asset.PublicUrl ?? $"/campaign-assets/{asset.Id}",
            ContentType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            CreatedAt = new DateTimeOffset(asset.CreatedAt, TimeSpan.Zero)
        };
    }

    private static CampaignCreativeSystemResponse ToResponse(CampaignCreativeSystem creativeSystem)
    {
        return new CampaignCreativeSystemResponse
        {
            Id = creativeSystem.Id,
            Prompt = creativeSystem.Prompt,
            IterationLabel = creativeSystem.IterationLabel,
            CreatedAt = new DateTimeOffset(creativeSystem.CreatedAt, TimeSpan.Zero),
            Output = DeserializeCreativeSystem(creativeSystem.OutputJson)
        };
    }

    private static CampaignSupplierBookingResponse ToResponse(CampaignSupplierBooking booking)
    {
        return new CampaignSupplierBookingResponse
        {
            Id = booking.Id,
            SupplierOrStation = booking.SupplierOrStation,
            Channel = booking.Channel,
            BookingStatus = booking.BookingStatus,
            CommittedAmount = booking.CommittedAmount,
            BookedAt = booking.BookedAt.HasValue ? new DateTimeOffset(booking.BookedAt.Value, TimeSpan.Zero) : null,
            LiveFrom = booking.LiveFrom,
            LiveTo = booking.LiveTo,
            Notes = booking.Notes,
            ProofAsset = booking.ProofAsset is null ? null : ToResponse(booking.ProofAsset)
        };
    }

    private static CampaignDeliveryReportResponse ToResponse(CampaignDeliveryReport report)
    {
        return new CampaignDeliveryReportResponse
        {
            Id = report.Id,
            SupplierBookingId = report.SupplierBookingId,
            ReportType = report.ReportType,
            Headline = report.Headline,
            Summary = report.Summary,
            ReportedAt = report.ReportedAt.HasValue ? new DateTimeOffset(report.ReportedAt.Value, TimeSpan.Zero) : null,
            Impressions = report.Impressions,
            PlaysOrSpots = report.PlaysOrSpots,
            SpendDelivered = report.SpendDelivered,
            EvidenceAsset = report.EvidenceAsset is null ? null : ToResponse(report.EvidenceAsset)
        };
    }

    private static IReadOnlyList<CampaignRecommendation> GetCurrentRecommendationSet(Campaign campaign)
    {
        return RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
    }

    private static (string ProposalLabel, string ProposalStrategy) GetProposalDetails(string? recommendationType)
    {
        var variantKey = recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => ("Proposal A", "Balanced mix"),
            "ooh_focus" => ("Proposal B", "OOH-led reach"),
            "radio_focus" => ("Proposal C", "Radio-led frequency"),
            "digital_focus" => ("Proposal C", "Digital-led amplification"),
            _ => ("Proposal", "Recommendation option")
        };
    }

    private static int GetProposalRank(string? recommendationType)
    {
        var variantKey = recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => 0,
            "ooh_focus" => 1,
            "radio_focus" => 2,
            "digital_focus" => 2,
            _ => 9
        };
    }

    private static RecommendationItemResponse ToResponse(RecommendationItem item, bool includeLinePricing)
    {
        var normalized = NormalizeRecommendationItemMetadata(item);

        return new RecommendationItemResponse
        {
            Id = item.Id,
            SourceInventoryId = item.InventoryItemId?.ToString() ?? normalized.SourceInventoryId,
            Region = normalized.Region,
            Language = normalized.Language,
            ShowDaypart = normalized.ShowDaypart,
            TimeBand = normalized.TimeBand,
            SlotType = normalized.SlotType,
            Duration = normalized.Duration,
            Restrictions = normalized.Restrictions,
            ConfidenceScore = normalized.ConfidenceScore,
            SelectionReasons = normalized.SelectionReasons,
            PolicyFlags = normalized.PolicyFlags,
            Quantity = item.Quantity,
            Flighting = normalized.Flighting,
            ItemNotes = normalized.ItemNotes,
            Dimensions = normalized.Dimensions,
            Material = normalized.Material,
            Illuminated = normalized.Illuminated,
            TrafficCount = normalized.TrafficCount,
            SiteNumber = normalized.SiteNumber,
            StartDate = normalized.StartDate,
            EndDate = normalized.EndDate,
            Title = item.DisplayName,
            Channel = item.InventoryType,
            Rationale = normalized.Rationale,
            Cost = includeLinePricing ? item.TotalCost : 0m,
            Type = item.InventoryType.Contains("upsell", StringComparison.OrdinalIgnoreCase) ? "upsell" : "base"
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
                return ReadJsonValue(value);
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("Metadata", out var nestedMetadata) &&
                nestedMetadata.ValueKind == JsonValueKind.Object &&
                nestedMetadata.TryGetProperty(propertyName, out var nestedValue))
            {
                return ReadJsonValue(nestedValue);
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static NormalizedRecommendationItemMetadata NormalizeRecommendationItemMetadata(RecommendationItem item)
    {
        var rationale = ExtractRationale(item.MetadataJson);
        var region = ExtractMetadataValue(item.MetadataJson, "region")
            ?? InferRegion(item.DisplayName, rationale);
        var language = ExtractMetadataValue(item.MetadataJson, "language")
            ?? InferLanguage(item.DisplayName, rationale);
        var showDaypart = ExtractMetadataValue(item.MetadataJson, "showDaypart")
            ?? ExtractMetadataValue(item.MetadataJson, "show_daypart")
            ?? ExtractMetadataValue(item.MetadataJson, "daypart")
            ?? InferShowDaypart(item.DisplayName, rationale);
        var timeBand = ExtractMetadataValue(item.MetadataJson, "timeBand")
            ?? ExtractMetadataValue(item.MetadataJson, "time_band")
            ?? InferTimeBand(item.DisplayName, rationale, showDaypart);
        var slotType = ExtractMetadataValue(item.MetadataJson, "slotType")
            ?? ExtractMetadataValue(item.MetadataJson, "slot_type")
            ?? InferSlotType(item.InventoryType, item.DisplayName, rationale);
        var duration = NormalizeDuration(
            ExtractMetadataValue(item.MetadataJson, "duration")
            ?? ExtractMetadataValue(item.MetadataJson, "durationSeconds")
            ?? ExtractMetadataValue(item.MetadataJson, "duration_seconds")
            ?? InferDuration(item.DisplayName, rationale));
        var restrictions = ExtractMetadataValue(item.MetadataJson, "restrictions")
            ?? ExtractMetadataValue(item.MetadataJson, "restrictionNotes")
            ?? InferRestrictions(rationale);

        return new NormalizedRecommendationItemMetadata(
            SourceInventoryId: ExtractMetadataValue(item.MetadataJson, "sourceInventoryId"),
            Region: region,
            Language: language,
            ShowDaypart: showDaypart,
            TimeBand: timeBand,
            SlotType: slotType,
            Duration: duration,
            Restrictions: restrictions,
            ConfidenceScore: ExtractDecimalMetadataValue(item.MetadataJson, "confidenceScore"),
            SelectionReasons: ExtractMetadataValues(item.MetadataJson, "selectionReasons"),
            PolicyFlags: ExtractMetadataValues(item.MetadataJson, "policyFlags"),
            Flighting: ExtractMetadataValue(item.MetadataJson, "flighting"),
            ItemNotes: ExtractMetadataValue(item.MetadataJson, "itemNotes"),
            Dimensions: ExtractMetadataValue(item.MetadataJson, "dimensions"),
            Material: ExtractMetadataValue(item.MetadataJson, "material"),
            Illuminated: ExtractMetadataValue(item.MetadataJson, "illuminated"),
            TrafficCount: ExtractMetadataValue(item.MetadataJson, "trafficCount") ?? ExtractMetadataValue(item.MetadataJson, "traffic_count"),
            SiteNumber: ExtractMetadataValue(item.MetadataJson, "siteNumber") ?? ExtractMetadataValue(item.MetadataJson, "site_number"),
            StartDate: ExtractMetadataValue(item.MetadataJson, "startDate"),
            EndDate: ExtractMetadataValue(item.MetadataJson, "endDate"),
            Rationale: rationale);
    }

    private static decimal? ExtractDecimalMetadataValue(string? metadataJson, string propertyName)
    {
        var raw = ExtractMetadataValue(metadataJson, propertyName);
        return decimal.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ExtractMetadataValues(string? metadataJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (TryExtractStringArray(doc.RootElement, propertyName, out var values))
            {
                return values;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("Metadata", out var nestedMetadata) &&
                TryExtractStringArray(nestedMetadata, propertyName, out values))
            {
                return values;
            }
        }
        catch (JsonException)
        {
        }

        return Array.Empty<string>();
    }

    private static string? InferRegion(string title, string rationale)
    {
        var explicitRegion = ExtractPipeSegment(rationale, "Region");
        if (!string.IsNullOrWhiteSpace(explicitRegion))
        {
            return explicitRegion;
        }

        var knownRegions = new[]
        {
            "Gauteng",
            "Johannesburg",
            "Sandton",
            "Pretoria",
            "Western Cape",
            "Cape Town",
            "KwaZulu-Natal",
            "Durban",
            "Eastern Cape",
            "Port Elizabeth",
            "National"
        };

        return knownRegions.FirstOrDefault(region =>
            title.Contains(region, StringComparison.OrdinalIgnoreCase)
            || rationale.Contains(region, StringComparison.OrdinalIgnoreCase));
    }

    private static string? InferLanguage(string title, string rationale)
    {
        var explicitLanguage = ExtractPipeSegment(rationale, "Language");
        if (!string.IsNullOrWhiteSpace(explicitLanguage))
        {
            return explicitLanguage;
        }

        var knownLanguages = new[]
        {
            "English",
            "Zulu",
            "Xhosa",
            "Afrikaans",
            "Xitsonga",
            "Sotho",
            "Tswana",
            "Venda"
        };

        return knownLanguages.FirstOrDefault(language =>
            title.Contains(language, StringComparison.OrdinalIgnoreCase)
            || rationale.Contains(language, StringComparison.OrdinalIgnoreCase));
    }

    private static string? InferShowDaypart(string title, string rationale)
    {
        var explicitDaypart = ExtractPipeSegment(rationale, "Show")
            ?? ExtractPipeSegment(rationale, "Show / Daypart");
        if (!string.IsNullOrWhiteSpace(explicitDaypart))
        {
            return explicitDaypart;
        }

        if (title.Contains("breakfast", StringComparison.OrdinalIgnoreCase) || rationale.Contains("breakfast", StringComparison.OrdinalIgnoreCase))
        {
            return "Breakfast";
        }

        if (title.Contains("drive", StringComparison.OrdinalIgnoreCase) || rationale.Contains("drive", StringComparison.OrdinalIgnoreCase))
        {
            return "Drive time";
        }

        if (title.Contains("workzone", StringComparison.OrdinalIgnoreCase) || rationale.Contains("workzone", StringComparison.OrdinalIgnoreCase))
        {
            return "Workzone";
        }

        if (title.Contains("midday", StringComparison.OrdinalIgnoreCase) || rationale.Contains("midday", StringComparison.OrdinalIgnoreCase))
        {
            return "Midday";
        }

        if (title.Contains("all day", StringComparison.OrdinalIgnoreCase) || rationale.Contains("all day", StringComparison.OrdinalIgnoreCase))
        {
            return "All day";
        }

        return null;
    }

    private static string? InferTimeBand(string title, string rationale, string? showDaypart)
    {
        var explicitTimeBand = ExtractPipeSegment(rationale, "Time")
            ?? ExtractPipeSegment(rationale, "Time band");
        if (!string.IsNullOrWhiteSpace(explicitTimeBand))
        {
            return explicitTimeBand;
        }

        if (showDaypart is not null)
        {
            if (showDaypart.Contains("breakfast", StringComparison.OrdinalIgnoreCase))
            {
                return "06:00-09:00";
            }

            if (showDaypart.Contains("drive", StringComparison.OrdinalIgnoreCase))
            {
                return "15:00-18:00";
            }

            if (showDaypart.Contains("midday", StringComparison.OrdinalIgnoreCase) || showDaypart.Contains("workzone", StringComparison.OrdinalIgnoreCase))
            {
                return "09:00-15:00";
            }

            if (showDaypart.Contains("all day", StringComparison.OrdinalIgnoreCase))
            {
                return "00:00-23:59";
            }
        }

        if (title.Contains("digital", StringComparison.OrdinalIgnoreCase) || rationale.Contains("digital", StringComparison.OrdinalIgnoreCase))
        {
            return "00:00-23:59";
        }

        return null;
    }

    private static string? InferSlotType(string inventoryType, string title, string rationale)
    {
        var explicitSlotType = ExtractPipeSegment(rationale, "Slot")
            ?? ExtractPipeSegment(rationale, "Slot type");
        if (!string.IsNullOrWhiteSpace(explicitSlotType))
        {
            return explicitSlotType;
        }

        if (rationale.Contains("live read", StringComparison.OrdinalIgnoreCase) || title.Contains("live read", StringComparison.OrdinalIgnoreCase))
        {
            return "Live read";
        }

        if (inventoryType.Contains("radio", StringComparison.OrdinalIgnoreCase))
        {
            return "Radio spot";
        }

        if (inventoryType.Contains("ooh", StringComparison.OrdinalIgnoreCase))
        {
            return "Placement";
        }

        if (inventoryType.Contains("digital", StringComparison.OrdinalIgnoreCase))
        {
            return "Digital slot";
        }

        return null;
    }

    private static string? InferDuration(string title, string rationale)
    {
        var explicitDuration = ExtractPipeSegment(rationale, "Duration");
        if (!string.IsNullOrWhiteSpace(explicitDuration))
        {
            return explicitDuration;
        }

        var search = $"{title} {rationale}";
        var values = new[] { "10s", "15s", "30s", "45s", "60s" };
        return values.FirstOrDefault(value => search.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string? InferRestrictions(string rationale)
    {
        var explicitRestrictions = ExtractPipeSegment(rationale, "Restrictions");
        return string.IsNullOrWhiteSpace(explicitRestrictions) ? null : explicitRestrictions;
    }

    private static string? ExtractPipeSegment(string rationale, string label)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        var parts = rationale
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .ToArray();

        foreach (var part in parts)
        {
            if (part.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase))
            {
                var value = part[(label.Length + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    private static string? NormalizeDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        var trimmed = duration.Trim();
        if (int.TryParse(trimmed, out var seconds) && seconds > 0)
        {
            return $"{seconds}s";
        }

        return trimmed;
    }

    private static bool TryExtractStringArray(JsonElement element, string propertyName, out IReadOnlyList<string> values)
    {
        values = Array.Empty<string>();
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            values = property
                .EnumerateArray()
                .Select(ReadJsonValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();
            return true;
        }

        var single = ReadJsonValue(property);
        if (!string.IsNullOrWhiteSpace(single))
        {
            values = new[] { single };
            return true;
        }

        return false;
    }

    private static string? ReadJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.ToString()
        };
    }

    private static string RemoveInternalMarkers(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return string.Empty;
        }

        var cleanedLines = rationale
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line =>
                !line.StartsWith(ClientFeedbackMarker, StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith(FallbackFlagsMarker, StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith(ManualReviewMarker, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return cleanedLines.Length == 0
            ? string.Empty
            : string.Join(Environment.NewLine, cleanedLines);
    }

    private static CreativeSystemResponse DeserializeCreativeSystem(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CreativeSystemResponse();
        }

        try
        {
            return JsonSerializer.Deserialize<CreativeSystemResponse>(json) ?? new CreativeSystemResponse();
        }
        catch (JsonException)
        {
            return new CreativeSystemResponse();
        }
    }

    private static bool ExtractManualReviewRequired(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return false;
        }

        var line = rationale
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .LastOrDefault(entry => entry.StartsWith(ManualReviewMarker, StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return false;
        }

        var rawValue = line[(ManualReviewMarker.Length)..].Trim();
        return bool.TryParse(rawValue, out var parsed) && parsed;
    }

    private static IReadOnlyList<string> ExtractFallbackFlags(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return Array.Empty<string>();
        }

        var line = rationale
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .LastOrDefault(entry => entry.StartsWith(FallbackFlagsMarker, StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return Array.Empty<string>();
        }

        return line[(FallbackFlagsMarker.Length)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(flag => flag.Trim())
            .Where(flag => !string.IsNullOrWhiteSpace(flag))
            .ToArray();
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

    private sealed record NormalizedRecommendationItemMetadata(
        string? SourceInventoryId,
        string? Region,
        string? Language,
        string? ShowDaypart,
        string? TimeBand,
        string? SlotType,
        string? Duration,
        string? Restrictions,
        decimal? ConfidenceScore,
        IReadOnlyList<string> SelectionReasons,
        IReadOnlyList<string> PolicyFlags,
        string? Flighting,
        string? ItemNotes,
        string? Dimensions,
        string? Material,
        string? Illuminated,
        string? TrafficCount,
        string? SiteNumber,
        string? StartDate,
        string? EndDate,
        string Rationale);
}

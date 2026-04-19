using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Creative;
using Advertified.App.Contracts.Packages;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data.Entities;
using Advertified.App.Support;
using CampaignPlanningRequestSnapshot = Advertified.App.Domain.Campaigns.CampaignPlanningRequestSnapshot;
using PlanningBudgetAllocationSnapshot = Advertified.App.Domain.Campaigns.PlanningBudgetAllocationSnapshot;
using RecommendationRejectedCandidateTrace = Advertified.App.Domain.Campaigns.RecommendationRejectedCandidateTrace;
using RecommendationSelectedItemTrace = Advertified.App.Domain.Campaigns.RecommendationSelectedItemTrace;

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
            PaymentProvider = campaign.PackageOrder.PaymentProvider ?? string.Empty,
            PaymentStatus = campaign.PackageOrder.PaymentStatus,
            CampaignName = campaign.CampaignName,
            Status = campaign.Status,
            PlanningMode = campaign.PlanningMode,
            AiUnlocked = campaign.AiUnlocked,
            AgentAssistanceRequested = campaign.AgentAssistanceRequested,
            UserId = campaign.UserId,
            ClientName = campaign.ResolveClientName(),
            ClientEmail = campaign.ResolveClientEmail(),
            BusinessName = campaign.ResolveBusinessName(),
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
        var recommendations = RecommendationSelectionPolicy.GetVisibleRecommendationSet(campaign)
            .Select((recommendation, index) => ToResponse(campaign, recommendation, includeLinePricing, index))
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
            ClientName = campaign.ResolveClientName(),
            ClientEmail = campaign.ResolveClientEmail(),
            BusinessName = campaign.ResolveBusinessName(),
            Industry = campaign.ResolveIndustry(),
            PackageOrderId = campaign.PackageOrderId,
            PackageBandId = campaign.PackageBandId,
            PackageBandName = campaign.PackageBand.Name,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
            PaymentProvider = campaign.PackageOrder.PaymentProvider ?? string.Empty,
            PaymentStatus = campaign.PackageOrder.PaymentStatus,
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
            ProspectDisposition = new ProspectDispositionResponse
            {
                Status = campaign.ProspectDispositionStatus,
                ReasonCode = campaign.ProspectDispositionReason,
                Notes = campaign.ProspectDispositionNotes,
                ClosedAt = campaign.ProspectDispositionClosedAt.HasValue ? new DateTimeOffset(campaign.ProspectDispositionClosedAt.Value, TimeSpan.Zero) : null,
                ClosedByUserId = campaign.ProspectDispositionClosedByUserId,
                ClosedByName = campaign.ProspectDispositionClosedByUser?.FullName
            },
            Workflow = CampaignWorkflowPolicy.BuildClientWorkflow(campaign),
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
            ExecutionTasks = campaign.CampaignExecutionTasks
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(ToResponse)
                .ToArray(),
            PerformanceTimeline = BuildPerformanceTimeline(campaign.CampaignDeliveryReports),
            EffectiveEndDate = schedule.EffectiveEndDate,
            DaysLeft = schedule.DaysLeft
        };
    }

    public static CampaignPerformanceSnapshotResponse ToPerformanceSnapshot(this Campaign campaign)
    {
        var bookingById = campaign.CampaignSupplierBookings
            .ToDictionary(item => item.Id, item => item);
        var channels = new Dictionary<string, CampaignPerformanceChannelResponse>(StringComparer.OrdinalIgnoreCase);
        var metricsByChannel = campaign.CampaignChannelMetrics
            .GroupBy(item => NormalizeChannel(item.Channel))
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var booking in campaign.CampaignSupplierBookings)
        {
            var channel = NormalizeChannel(booking.Channel);
            var current = GetOrCreateChannel(channels, channel);
            current.BookedSpend += booking.CommittedAmount;
            current.BookingCount += 1;
        }

        foreach (var report in campaign.CampaignDeliveryReports)
        {
            var channel = ResolveChannelForReport(report, bookingById);
            var current = GetOrCreateChannel(channels, channel);
            current.DeliveredSpend += report.SpendDelivered ?? 0m;
            current.Impressions += report.Impressions ?? 0;
            current.PlaysOrSpots += report.PlaysOrSpots ?? 0;
            current.ReportCount += 1;
            if (string.Equals(report.ReportType, CampaignPerformanceConstants.SyncedReportType, StringComparison.OrdinalIgnoreCase))
            {
                current.SyncedClicks += report.PlaysOrSpots ?? 0;
            }
        }

        foreach (var channelMetrics in metricsByChannel)
        {
            var channel = channelMetrics.Key;
            var current = GetOrCreateChannel(channels, channel);
            var metricRows = channelMetrics.Value;
            var leads = metricRows.Sum(item => item.Leads);
            var spend = metricRows.Sum(item => item.SpendZar);
            var attributedRevenue = metricRows.Sum(item => item.AttributedRevenueZar);

            current.Leads += leads;
            current.CplZar = leads > 0
                ? decimal.Round(spend / leads, 2, MidpointRounding.AwayFromZero)
                : null;
            current.Roas = spend > 0m && attributedRevenue > 0m
                ? decimal.Round(attributedRevenue / spend, 4, MidpointRounding.AwayFromZero)
                : null;
        }

        var totalBookedSpend = campaign.CampaignSupplierBookings.Sum(item => item.CommittedAmount);
        var totalDeliveredSpend = campaign.CampaignDeliveryReports.Sum(item => item.SpendDelivered ?? 0m);
        var totalImpressions = campaign.CampaignDeliveryReports.Sum(item => item.Impressions ?? 0);
        var totalPlaysOrSpots = campaign.CampaignDeliveryReports.Sum(item => item.PlaysOrSpots ?? 0);
        var totalLeads = campaign.CampaignChannelMetrics.Sum(item => item.Leads);
        var totalMetricSpend = campaign.CampaignChannelMetrics.Sum(item => item.SpendZar);
        var totalAttributedRevenue = campaign.CampaignChannelMetrics.Sum(item => item.AttributedRevenueZar);
        var totalSyncedClicks = campaign.CampaignDeliveryReports
            .Where(item => string.Equals(item.ReportType, CampaignPerformanceConstants.SyncedReportType, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.PlaysOrSpots ?? 0);
        var latestDeliveryDate = campaign.CampaignDeliveryReports
            .Where(item => item.ReportedAt.HasValue)
            .Select(item => DateOnly.FromDateTime(item.ReportedAt!.Value))
            .OrderBy(item => item)
            .LastOrDefault();
        var latestMetricDate = campaign.CampaignChannelMetrics
            .Select(item => item.MetricDate)
            .OrderBy(item => item)
            .LastOrDefault();
        var latestReportDate = latestMetricDate > latestDeliveryDate ? latestMetricDate : latestDeliveryDate;
        var avgCpl = totalLeads > 0
            ? decimal.Round(totalMetricSpend / totalLeads, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        var avgRoas = totalMetricSpend > 0m && totalAttributedRevenue > 0m
            ? decimal.Round(totalAttributedRevenue / totalMetricSpend, 4, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        return new CampaignPerformanceSnapshotResponse
        {
            CampaignId = campaign.Id,
            TotalBookedSpend = totalBookedSpend,
            TotalDeliveredSpend = totalDeliveredSpend,
            TotalImpressions = totalImpressions,
            TotalPlaysOrSpots = totalPlaysOrSpots,
            TotalLeads = totalLeads,
            AverageCplZar = avgCpl,
            AverageRoas = avgRoas,
            TotalSyncedClicks = totalSyncedClicks,
            BookingCount = campaign.CampaignSupplierBookings.Count,
            ReportCount = campaign.CampaignDeliveryReports.Count,
            SpendDeliveryPercent = ClampPercent(totalBookedSpend > 0m ? (totalDeliveredSpend / totalBookedSpend) * 100m : 0m),
            LatestReportDate = latestReportDate == default ? null : latestReportDate,
            Timeline = BuildPerformanceTimeline(campaign.CampaignDeliveryReports, campaign.CampaignChannelMetrics),
            Channels = channels.Values
                .OrderByDescending(item => item.DeliveredSpend + item.BookedSpend)
                .ThenByDescending(item => item.Impressions + item.PlaysOrSpots)
                .ToArray()
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
            BusinessStage = brief.BusinessStage,
            MonthlyRevenueBand = brief.MonthlyRevenueBand,
            SalesModel = brief.SalesModel,
            StartDate = brief.StartDate,
            EndDate = brief.EndDate,
            DurationWeeks = brief.DurationWeeks,
            ChannelFlights = CampaignFlightingSupport.Deserialize(brief.ChannelFlightsJson),
            GeographyScope = brief.GeographyScope,
            Provinces = DeserializeList(brief.ProvincesJson),
            Cities = DeserializeList(brief.CitiesJson),
            Suburbs = DeserializeList(brief.SuburbsJson),
            Areas = DeserializeList(brief.AreasJson),
            TargetLocationLabel = brief.TargetLocationLabel,
            TargetLocationCity = brief.TargetLocationCity,
            TargetLocationProvince = brief.TargetLocationProvince,
            TargetLatitude = brief.TargetLatitude,
            TargetLongitude = brief.TargetLongitude,
            TargetAgeMin = brief.TargetAgeMin,
            TargetAgeMax = brief.TargetAgeMax,
            TargetGender = brief.TargetGender,
            TargetLanguages = DeserializeList(brief.TargetLanguagesJson),
            TargetLsmMin = brief.TargetLsmMin,
            TargetLsmMax = brief.TargetLsmMax,
            TargetInterests = DeserializeList(brief.TargetInterestsJson),
            TargetAudienceNotes = brief.TargetAudienceNotes,
            CustomerType = brief.CustomerType,
            BuyingBehaviour = brief.BuyingBehaviour,
            DecisionCycle = brief.DecisionCycle,
            PricePositioning = brief.PricePositioning,
            AverageCustomerSpendBand = brief.AverageCustomerSpendBand,
            GrowthTarget = brief.GrowthTarget,
            UrgencyLevel = brief.UrgencyLevel,
            AudienceClarity = brief.AudienceClarity,
            ValuePropositionFocus = brief.ValuePropositionFocus,
            PreferredMediaTypes = DeserializeList(brief.PreferredMediaTypesJson),
            ExcludedMediaTypes = DeserializeList(brief.ExcludedMediaTypesJson),
            MustHaveAreas = DeserializeList(brief.MustHaveAreasJson),
            ExcludedAreas = DeserializeList(brief.ExcludedAreasJson),
            CreativeReady = brief.CreativeReady,
            CreativeNotes = brief.CreativeNotes,
            MaxMediaItems = brief.MaxMediaItems,
            OpenToUpsell = brief.OpenToUpsell,
            AdditionalBudget = brief.AdditionalBudget,
            SpecialRequirements = brief.SpecialRequirements,
            PreferredVideoAspectRatio = brief.PreferredVideoAspectRatio,
            PreferredVideoDurationSeconds = brief.PreferredVideoDurationSeconds
        };
    }

    private static List<string>? DeserializeList(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<List<string>>(json);
    }

    private static CampaignRecommendationResponse ToResponse(Campaign campaign, CampaignRecommendation recommendation, bool includeLinePricing, int proposalIndex)
    {
        var extractedFeedback = ExtractClientFeedbackNotes(recommendation.Rationale);
        var fallbackFlags = ExtractFallbackFlags(recommendation.Rationale);
        var manualReviewRequired = ExtractManualReviewRequired(recommendation.Rationale);
        var (proposalLabel, proposalStrategy) = GetProposalDetails(recommendation.RecommendationType, proposalIndex);

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
            Audit = BuildAuditResponse(recommendation),
            Status = recommendation.Status,
            TotalCost = recommendation.TotalCost,
            EmailDeliveries = campaign.EmailDeliveryMessages
                .Where(message =>
                    string.Equals(message.DeliveryPurpose, "recommendation_ready", StringComparison.OrdinalIgnoreCase)
                    && message.RecommendationRevisionNumber == recommendation.RevisionNumber)
                .OrderByDescending(message => message.CreatedAt)
                .Select(ToResponse)
                .ToArray(),
            Items = recommendation.RecommendationItems
                .OrderBy(item => GetRecommendationItemChannelRank(item.InventoryType))
                .ThenBy(item => item.DisplayName)
                .Select(item => ToResponse(item, includeLinePricing))
                .ToArray()
        };
    }

    private static EmailDeliveryAttemptResponse ToResponse(EmailDeliveryMessage message)
    {
        return new EmailDeliveryAttemptResponse
        {
            Id = message.Id,
            Provider = message.ProviderKey,
            Purpose = message.DeliveryPurpose,
            TemplateName = message.TemplateName,
            Status = message.Status,
            RecipientEmail = message.RecipientEmail,
            Subject = message.Subject,
            LatestEventType = message.LatestEventType,
            LatestEventAt = message.LatestEventAt.HasValue ? new DateTimeOffset(message.LatestEventAt.Value, TimeSpan.Zero) : null,
            AcceptedAt = message.AcceptedAt.HasValue ? new DateTimeOffset(message.AcceptedAt.Value, TimeSpan.Zero) : null,
            DeliveredAt = message.DeliveredAt.HasValue ? new DateTimeOffset(message.DeliveredAt.Value, TimeSpan.Zero) : null,
            OpenedAt = message.OpenedAt.HasValue ? new DateTimeOffset(message.OpenedAt.Value, TimeSpan.Zero) : null,
            ClickedAt = message.ClickedAt.HasValue ? new DateTimeOffset(message.ClickedAt.Value, TimeSpan.Zero) : null,
            BouncedAt = message.BouncedAt.HasValue ? new DateTimeOffset(message.BouncedAt.Value, TimeSpan.Zero) : null,
            FailedAt = message.FailedAt.HasValue ? new DateTimeOffset(message.FailedAt.Value, TimeSpan.Zero) : null,
            LastError = message.LastError
        };
    }

    private static CampaignRecommendationAuditResponse? BuildAuditResponse(CampaignRecommendation recommendation)
    {
        var audit = recommendation.RecommendationRunAudits
            .OrderByDescending(entry => entry.CreatedAt)
            .FirstOrDefault();
        if (audit is null)
        {
            return null;
        }

        var request = DeserializeAuditJson<CampaignPlanningRequestSnapshot>(audit.RequestSnapshotJson) ?? new CampaignPlanningRequestSnapshot();
        var selectedItems = DeserializeAuditJson<List<RecommendationSelectedItemTrace>>(audit.SelectedItemsJson) ?? new List<RecommendationSelectedItemTrace>();
        var rejectedItems = DeserializeAuditJson<List<RecommendationRejectedCandidateTrace>>(audit.RejectedCandidatesJson) ?? new List<RecommendationRejectedCandidateTrace>();
        var fallback = DeserializeAuditJson<RecommendationFallbackAuditSnapshot>(audit.FallbackFlagsJson) ?? new RecommendationFallbackAuditSnapshot();

        return new CampaignRecommendationAuditResponse
        {
            RequestSummary = BuildRequestSummary(request),
            SelectionSummary = BuildSelectionSummary(selectedItems),
            RejectionSummary = BuildRejectionSummary(rejectedItems),
            PolicySummary = BuildPolicySummary(rejectedItems, DeserializeAuditJson<PlanningPolicyOptions>(audit.PolicySnapshotJson)),
            BudgetSummary = BuildBudgetSummary(request.SelectedBudget, recommendation.TotalCost, audit.BudgetUtilizationRatio),
            FallbackSummary = BuildFallbackSummary(fallback.FallbackFlags)
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

    private static CampaignExecutionTaskResponse ToResponse(CampaignExecutionTask task)
    {
        return new CampaignExecutionTaskResponse
        {
            Id = task.Id,
            TaskKey = task.TaskKey,
            Title = task.Title,
            Details = task.Details,
            Status = task.Status,
            SortOrder = task.SortOrder,
            DueAt = task.DueAt.HasValue ? new DateTimeOffset(task.DueAt.Value, TimeSpan.Zero) : null,
            CompletedAt = task.CompletedAt.HasValue ? new DateTimeOffset(task.CompletedAt.Value, TimeSpan.Zero) : null
        };
    }

    private static IReadOnlyList<CampaignPerformanceTimelinePointResponse> BuildPerformanceTimeline(
        IEnumerable<CampaignDeliveryReport> reports)
    {
        return reports
            .Where(report => report.ReportedAt.HasValue)
            .GroupBy(report => DateOnly.FromDateTime(report.ReportedAt!.Value))
            .Select(group => new CampaignPerformanceTimelinePointResponse
            {
                Date = group.Key,
                Impressions = group.Sum(item => item.Impressions ?? 0),
                PlaysOrSpots = group.Sum(item => item.PlaysOrSpots ?? 0),
                SpendDelivered = group.Sum(item => item.SpendDelivered ?? 0m)
            })
            .OrderBy(item => item.Date)
            .ToArray();
    }

    private static IReadOnlyList<CampaignPerformanceTimelinePointResponse> BuildPerformanceTimeline(
        IEnumerable<CampaignDeliveryReport> reports,
        IEnumerable<CampaignChannelMetric> channelMetrics)
    {
        var metricRows = channelMetrics.ToArray();
        if (metricRows.Length == 0)
        {
            return BuildPerformanceTimeline(reports);
        }

        var deliveryRows = reports
            .Where(report => report.ReportedAt.HasValue)
            .ToArray();

        var timeline = metricRows
            .GroupBy(item => item.MetricDate)
            .Select(group =>
            {
                var groupedDelivery = deliveryRows
                    .Where(report => DateOnly.FromDateTime(report.ReportedAt!.Value) == group.Key)
                    .ToArray();
                var spend = group.Sum(item => item.SpendZar);
                var leads = group.Sum(item => item.Leads);
                var attributedRevenue = group.Sum(item => item.AttributedRevenueZar);

                return new CampaignPerformanceTimelinePointResponse
                {
                    Date = group.Key,
                    Impressions = group.Sum(item => item.Impressions) + groupedDelivery.Sum(item => item.Impressions ?? 0),
                    PlaysOrSpots = group.Sum(item => item.Clicks) + groupedDelivery.Sum(item => item.PlaysOrSpots ?? 0),
                    Leads = leads,
                    CplZar = leads > 0
                        ? decimal.Round(spend / leads, 2, MidpointRounding.AwayFromZero)
                        : null,
                    Roas = spend > 0m && attributedRevenue > 0m
                        ? decimal.Round(attributedRevenue / spend, 4, MidpointRounding.AwayFromZero)
                        : null,
                    SpendDelivered = spend + groupedDelivery.Sum(item => item.SpendDelivered ?? 0m)
                };
            })
            .OrderBy(item => item.Date)
            .ToArray();

        return timeline;
    }

    private static CampaignPerformanceChannelResponse GetOrCreateChannel(
        IDictionary<string, CampaignPerformanceChannelResponse> channels,
        string channel)
    {
        if (channels.TryGetValue(channel, out var existing))
        {
            return existing;
        }

        var created = new CampaignPerformanceChannelResponse
        {
            Channel = channel,
            Label = BuildChannelLabel(channel)
        };
        channels[channel] = created;
        return created;
    }

    private static string ResolveChannelForReport(
        CampaignDeliveryReport report,
        IReadOnlyDictionary<Guid, CampaignSupplierBooking> bookingById)
    {
        if (report.SupplierBookingId.HasValue
            && bookingById.TryGetValue(report.SupplierBookingId.Value, out var booking))
        {
            return NormalizeChannel(booking.Channel);
        }

        return "unknown";
    }

    private static string NormalizeChannel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string BuildChannelLabel(string channel)
    {
        return channel switch
        {
            "ooh" => "Billboards and Digital Screens",
            "tv" => "TV",
            "unknown" => "Unknown",
            _ => string.Join(' ', channel.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]))
        };
    }

    private static int ClampPercent(decimal value)
    {
        if (value <= 0m)
        {
            return 0;
        }

        if (value >= 100m)
        {
            return 100;
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
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

    private static (string ProposalLabel, string ProposalStrategy) GetProposalDetails(string? recommendationType, int proposalIndex)
    {
        var variantKey = recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => ("Proposal A", "Balanced mix"),
            "ooh_focus" => ("Proposal B", "Billboards and Digital Screens-led reach"),
            "radio_focus" => ("Proposal C", "Radio-led frequency"),
            "digital_focus" => ("Proposal C", "Digital-led amplification"),
            "tv_focus" => ("Proposal C", "TV-led reach"),
            _ => ($"Proposal {GetProposalLetter(proposalIndex)}", "Recommendation option")
        };
    }

    private static string GetProposalLetter(int index)
    {
        return index >= 0 && index < 26
            ? ((char)('A' + index)).ToString()
            : (index + 1).ToString();
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
            "tv_focus" => 2,
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
            AppliedDuration = normalized.AppliedDuration,
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
            RequestedStartDate = normalized.RequestedStartDate,
            RequestedEndDate = normalized.RequestedEndDate,
            ResolvedStartDate = normalized.ResolvedStartDate,
            ResolvedEndDate = normalized.ResolvedEndDate,
            CommercialExplanation = normalized.CommercialExplanation,
            DurationFitScore = normalized.DurationFitScore,
            Title = item.DisplayName,
            Channel = item.InventoryType,
            Rationale = normalized.Rationale,
            Cost = includeLinePricing ? item.TotalCost : 0m,
            Type = item.InventoryType.Contains("upsell", StringComparison.OrdinalIgnoreCase) ? "upsell" : "base"
        };
    }

    private static int GetRecommendationItemChannelRank(string? channel)
    {
        var normalized = NormalizeRecommendationChannel(channel);
        return normalized switch
        {
            "ooh" => 0,
            "radio" => 1,
            "tv" => 2,
            "digital" => 3,
            _ => 9
        };
    }

    private static string NormalizeRecommendationChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("ooh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("billboard", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("out of home", StringComparison.OrdinalIgnoreCase))
        {
            return "ooh";
        }

        if (normalized.Contains("radio", StringComparison.OrdinalIgnoreCase))
        {
            return "radio";
        }

        if (normalized.Contains("tv", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("television", StringComparison.OrdinalIgnoreCase))
        {
            return "tv";
        }

        if (normalized.Contains("digital", StringComparison.OrdinalIgnoreCase))
        {
            return "digital";
        }

        return normalized;
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
            ?? ExtractMetadataValue(item.MetadataJson, "dayType")
            ?? ExtractMetadataValue(item.MetadataJson, "day_type")
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
        var appliedDuration = NormalizeDuration(
            ExtractMetadataValue(item.MetadataJson, "appliedDuration")
            ?? ExtractMetadataValue(item.MetadataJson, "appliedDurationLabel")
            ?? ExtractMetadataValue(item.MetadataJson, "applied_duration_label"));
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
            AppliedDuration: appliedDuration,
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
            RequestedStartDate: ExtractMetadataValue(item.MetadataJson, "requestedStartDate") ?? ExtractMetadataValue(item.MetadataJson, "requested_start_date"),
            RequestedEndDate: ExtractMetadataValue(item.MetadataJson, "requestedEndDate") ?? ExtractMetadataValue(item.MetadataJson, "requested_end_date"),
            ResolvedStartDate: ExtractMetadataValue(item.MetadataJson, "resolvedStartDate") ?? ExtractMetadataValue(item.MetadataJson, "resolved_start_date"),
            ResolvedEndDate: ExtractMetadataValue(item.MetadataJson, "resolvedEndDate") ?? ExtractMetadataValue(item.MetadataJson, "resolved_end_date"),
            CommercialExplanation: ExtractMetadataValue(item.MetadataJson, "commercialExplanation") ?? ExtractMetadataValue(item.MetadataJson, "commercial_explanation"),
            DurationFitScore: ExtractDecimalMetadataValue(item.MetadataJson, "durationFitScore") ?? ExtractDecimalMetadataValue(item.MetadataJson, "duration_fit_score"),
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

    private static T? DeserializeAuditJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string BuildRequestSummary(CampaignPlanningRequestSnapshot request)
    {
        var objective = string.IsNullOrWhiteSpace(request.Objective) ? "Not specified" : request.Objective.Trim();
        var businessOrigin = request.BusinessLocation?.Area
            ?? request.BusinessLocation?.City
            ?? request.BusinessLocation?.Province;
        var originSuffix = string.IsNullOrWhiteSpace(businessOrigin) ? string.Empty : $", origin {businessOrigin.Trim()}";
        var allocationSuffix = request.BudgetAllocation is null
            ? string.Empty
            : $", allocation {BuildAuditAllocationLabel(request.BudgetAllocation)}";
        return $"Request: {objective}, {BuildAuditGeographyLabel(request)}{originSuffix}, target mix {BuildAuditMixLabel(request)}{allocationSuffix}.";
    }

    private static string BuildSelectionSummary(IReadOnlyList<RecommendationSelectedItemTrace> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return "Selected: no lines were recorded.";
        }

        var groupedChannels = selectedItems
            .GroupBy(item => FormatAuditChannelLabel(item.MediaType), StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Count()} {group.Key}")
            .ToArray();
        var topItem = selectedItems[0];
        var topReasons = topItem.SelectionReasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Take(2)
            .ToArray();

        var summary = $"Selected: {string.Join(", ", groupedChannels)}. Top line {topItem.DisplayName}.";
        if (topReasons.Length > 0)
        {
            summary += $" Reasons: {string.Join("; ", topReasons)}.";
        }

        return summary;
    }

    private static string BuildRejectionSummary(IReadOnlyList<RecommendationRejectedCandidateTrace> rejectedItems)
    {
        if (rejectedItems.Count == 0)
        {
            return "Rejected: none recorded.";
        }

        var grouped = rejectedItems
            .GroupBy(item => item.Reason, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(group => $"{FormatAuditReason(group.Key)} {group.Count()}")
            .ToArray();

        return $"Rejected: {string.Join(", ", grouped)}.";
    }

    private static string BuildPolicySummary(IReadOnlyList<RecommendationRejectedCandidateTrace> rejectedItems, PlanningPolicyOptions? snapshot)
    {
        var policyRejections = rejectedItems
            .Where(item => string.Equals(item.Stage, "policy", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Reason, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{FormatAuditReason(group.Key)} {group.Count()}")
            .ToArray();

        if (policyRejections.Length > 0)
        {
            return $"Policy: {string.Join(", ", policyRejections)}.";
        }

        if (snapshot is null)
        {
            return "Policy: no policy rejections recorded.";
        }

        return $"Policy: scale floor {FormatCurrency(snapshot.Scale.BudgetFloor)}, dominance floor {FormatCurrency(snapshot.Dominance.BudgetFloor)}.";
    }

    private static string BuildBudgetSummary(decimal selectedBudget, decimal totalCost, decimal utilizationRatio)
    {
        var ratio = utilizationRatio > 0m && utilizationRatio <= 1m
            ? utilizationRatio
            : (selectedBudget <= 0m ? 0m : totalCost / selectedBudget);
        var percentage = Math.Round(ratio * 100m, 0, MidpointRounding.AwayFromZero);
        return $"Budget: {FormatCurrency(totalCost)} of {FormatCurrency(selectedBudget)} used ({percentage:0}%).";
    }

    private static string? BuildFallbackSummary(IReadOnlyList<string> flags)
    {
        var meaningfulFlags = flags
            .Where(flag => !string.IsNullOrWhiteSpace(flag))
            .ToArray();
        if (meaningfulFlags.Length == 0)
        {
            return null;
        }

        return $"Fallback: {string.Join(", ", meaningfulFlags.Select(FormatAuditReason))}.";
    }

    private static string BuildAuditGeographyLabel(CampaignPlanningRequestSnapshot request)
    {
        var scope = request.Targeting?.Scope ?? request.GeographyScope;
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "unknown geography" : scope.Trim().ToLowerInvariant();
        var place = request.Targeting?.Label
            ?? request.TargetLocationLabel
            ?? request.Suburbs.FirstOrDefault()
            ?? request.Cities.FirstOrDefault()
            ?? request.Areas.FirstOrDefault()
            ?? request.Provinces.FirstOrDefault()
            ?? request.TargetLocationProvince;

        return string.IsNullOrWhiteSpace(place)
            ? normalizedScope
            : $"{normalizedScope} {place.Trim()}";
    }

    private static string BuildAuditMixLabel(CampaignPlanningRequestSnapshot request)
    {
        var parts = new List<string>();
        if (request.TargetOohShare.GetValueOrDefault() > 0) parts.Add($"Billboards and Digital Screens {request.TargetOohShare.GetValueOrDefault()}%");
        if (request.TargetRadioShare.GetValueOrDefault() > 0) parts.Add($"Radio {request.TargetRadioShare.GetValueOrDefault()}%");
        if (request.TargetTvShare.GetValueOrDefault() > 0) parts.Add($"TV {request.TargetTvShare.GetValueOrDefault()}%");
        if (request.TargetDigitalShare.GetValueOrDefault() > 0) parts.Add($"Digital {request.TargetDigitalShare.GetValueOrDefault()}%");

        if (parts.Count == 0 && request.BudgetAllocation?.ChannelAllocations.Count > 0)
        {
            foreach (var allocation in request.BudgetAllocation.ChannelAllocations
                .Where(entry => entry.Weight > 0m)
                .OrderByDescending(entry => entry.Weight))
            {
                parts.Add($"{FormatAuditChannelLabel(allocation.Channel)} {allocation.Weight:P0}");
            }
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : "not specified";
    }

    private static string BuildAuditAllocationLabel(PlanningBudgetAllocationSnapshot allocation)
    {
        var topChannel = allocation.ChannelAllocations.OrderByDescending(entry => entry.Weight).FirstOrDefault();
        var topGeo = allocation.GeoAllocations.OrderByDescending(entry => entry.Weight).FirstOrDefault();
        if (topChannel is null || topGeo is null)
        {
            return "not specified";
        }

        return $"{FormatAuditChannelLabel(topChannel.Channel)} {topChannel.Weight:P0}, {topGeo.Bucket} {topGeo.Weight:P0}";
    }

    private static string FormatAuditChannelLabel(string? mediaType)
    {
        return (mediaType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ooh" => "Billboards and Digital Screens",
            "tv" => "TV",
            "radio" => "Radio",
            "digital" => "Digital",
            _ => string.IsNullOrWhiteSpace(mediaType) ? "Unknown" : mediaType.Trim()
        };
    }

    private static string FormatAuditReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown";
        }

        return reason.Trim().ToLowerInvariant() switch
        {
            "candidate_unavailable" => "candidate unavailable",
            "cost_out_of_budget" => "cost out of budget",
            "media_type_excluded" => "media type excluded",
            "geography_mismatch" => "geography mismatch",
            "radio_not_national_capable" => "radio not national capable",
            "inventory_insufficient" => "inventory insufficient",
            "no_recommendation_generated" => "no recommendation generated",
            "policy_relaxed" => "policy relaxed",
            "national_radio_inventory_insufficient" => "national radio inventory insufficient",
            _ => reason.Replace('_', ' ')
        };
    }

    private static string FormatCurrency(decimal value)
    {
        return $"R {value:N2}";
    }

    private sealed record NormalizedRecommendationItemMetadata(
        string? SourceInventoryId,
        string? Region,
        string? Language,
        string? ShowDaypart,
        string? TimeBand,
        string? SlotType,
        string? Duration,
        string? AppliedDuration,
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
        string? RequestedStartDate,
        string? RequestedEndDate,
        string? ResolvedStartDate,
        string? ResolvedEndDate,
        string? CommercialExplanation,
        decimal? DurationFitScore,
        string Rationale);

    private sealed class RecommendationFallbackAuditSnapshot
    {
        public List<string> FallbackFlags { get; set; } = new();
        public List<string> SelectedChannels { get; set; } = new();
    }
}


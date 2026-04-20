using System.Text.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Advertified.App.Services;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly PlanningPolicySnapshotProvider _planningPolicySnapshotProvider;
    private readonly PlanningBudgetAllocationSnapshotProvider _planningBudgetAllocationSnapshotProvider;
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;

    public AdminDashboardService(
        AppDbContext db,
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        PlanningPolicySnapshotProvider planningPolicySnapshotProvider,
        PlanningBudgetAllocationSnapshotProvider planningBudgetAllocationSnapshotProvider,
        Npgsql.NpgsqlDataSource dataSource,
        IBroadcastMasterDataService broadcastMasterDataService)
    {
        _db = db;
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _planningPolicySnapshotProvider = planningPolicySnapshotProvider;
        _planningBudgetAllocationSnapshotProvider = planningBudgetAllocationSnapshotProvider;
        _dataSource = dataSource;
        _broadcastMasterDataService = broadcastMasterDataService;
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var outletRecords = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        var outlets = outletRecords
            .Select(MapOutlet)
            .OrderBy(x => x.MediaType)
            .ThenBy(x => x.Name)
            .ToArray();
        var outletMasterData = await _broadcastMasterDataService.GetOutletMasterDataAsync(cancellationToken);

        var packageSettings = await GetPackageSettingsAsync(cancellationToken);
        var pricingSettings = await GetPricingSettingsAsync(cancellationToken);
        var leadIndustryPolicies = await GetLeadIndustryPoliciesAsync(cancellationToken);

        var strongCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "strong", StringComparison.OrdinalIgnoreCase));
        var mixedCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "mixed_not_fully_healthy", StringComparison.OrdinalIgnoreCase));
        var weakUnpricedCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "weak_unpriced", StringComparison.OrdinalIgnoreCase));
        var weakNoInventoryCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "weak_no_inventory", StringComparison.OrdinalIgnoreCase));
        var weakOutletCount = outletRecords.Count(x => !string.Equals(DetermineHealthBucket(x), "strong", StringComparison.OrdinalIgnoreCase));
        var weakOutlets = outletRecords
            .Select(record => MapHealthIssue(record, outletMasterData))
            .Where(static issue => issue is not null)
            .Select(static issue => issue!)
            .Take(10)
            .ToArray();

        var sourceDocuments = await GetSourceDocumentCountAsync(cancellationToken);
        var recentImports = await GetRecentImportsAsync(cancellationToken);
        var areaMappings = await GetAreaMappingsAsync(cancellationToken);
        var auditEntries = await GetAuditEntriesAsync(cancellationToken);
        var users = await GetUsersAsync(areaMappings, cancellationToken);
        var integrations = await GetIntegrationsAsync(cancellationToken);
        var previewRules = await GetPreviewRulesAsync(cancellationToken);
        var monitoring = await GetMonitoringAsync(areaMappings.Count, cancellationToken);
        var fallbackRatePercent = await GetFallbackRatePercentAsync(cancellationToken);
        var aiQueueAlerts = monitoring.AiJobAlerts
            .Take(3)
            .Select(alert => new AdminAlertResponse
            {
                Title = $"AI {alert.Pipeline} job failed",
                Context = $"Campaign {alert.CampaignId} | Retries {alert.RetryAttemptCount} | {alert.LastFailure ?? "No failure message"}",
                Severity = alert.RetryAttemptCount >= 5 ? "Critical" : "Warning",
                Owner = "AI Platform"
            });
        var inventoryAlerts = weakOutlets
            .Take(3)
            .Select(issue => new AdminAlertResponse
            {
                Title = issue.Issue,
                Context = $"{issue.OutletName} | {issue.Impact}",
                Severity = issue.Issue.Contains("Missing", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Warning"
            });
        var combinedAlerts = aiQueueAlerts
            .Concat(inventoryAlerts)
            .Take(6)
            .ToArray();

        return new AdminDashboardResponse
        {
            Summary = new AdminSummaryResponse
            {
                ActiveOutlets = outlets.Length,
                WeakOutlets = weakOutletCount,
                SourceDocuments = sourceDocuments,
                FallbackRatePercent = fallbackRatePercent
            },
            Alerts = combinedAlerts,
            Outlets = outlets,
            RecentImports = recentImports,
            Health = new AdminHealthResponse
            {
                StrongCount = strongCount,
                MixedCount = mixedCount,
                WeakUnpricedCount = weakUnpricedCount,
                WeakNoInventoryCount = weakNoInventoryCount
            },
            HealthIssues = weakOutlets,
            Areas = areaMappings,
            PackageSettings = packageSettings,
            PricingSettings = pricingSettings,
            EnginePolicies = BuildEnginePolicies(),
            PlanningAllocationSettings = BuildPlanningAllocationSettings(),
            LeadIndustryPolicies = leadIndustryPolicies,
            PreviewRules = previewRules,
            Monitoring = monitoring,
            Users = users,
            AuditEntries = auditEntries,
            Integrations = integrations
        };
    }

    public async Task<AdminOutletPageResponse> GetOutletPageAsync(int page, int pageSize, bool issuesOnly, string sortBy, CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 10, 100);
        var normalizedSort = NormalizeOutletSort(sortBy);
        var outletRecords = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        var mappedOutlets = outletRecords.Select(MapOutlet).ToArray();
        var issueCount = mappedOutlets.Count(HasOutletIssue);
        var strongCount = mappedOutlets.Count(static item => item.CatalogHealth == "strong");
        var visibleOutlets = issuesOnly
            ? mappedOutlets.Where(HasOutletIssue)
            : mappedOutlets.AsEnumerable();
        var sortedOutlets = SortOutlets(visibleOutlets, normalizedSort).ToArray();
        var totalCount = sortedOutlets.Length;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var effectivePage = Math.Min(normalizedPage, totalPages);
        var items = sortedOutlets
            .Skip((effectivePage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();

        return new AdminOutletPageResponse
        {
            Items = items,
            Page = effectivePage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            IssueCount = issueCount,
            StrongCount = strongCount,
            IssuesOnly = issuesOnly,
            SortBy = normalizedSort
        };
    }

    private async Task<int> GetSourceDocumentCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from import_manifest;",
            cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<AdminImportDocumentResponse>> GetRecentImportsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            select
                im.source_file as SourceFile,
                im.channel as Channel,
                pdm.supplier_or_station as SupplierOrStation,
                coalesce(pdm.document_title, pdm.source_file) as DocumentTitle,
                pdm.please_note as Notes,
                im.page_count as PageCount,
                im.imported_at as ImportedAt
            from import_manifest im
            left join package_document_metadata pdm on pdm.source_file = im.source_file
            order by im.imported_at desc
            limit 12;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AdminImportDocumentResponse>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private async Task<IReadOnlyList<AdminAreaMappingResponse>> GetAreaMappingsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            select
                pap.cluster_code as Code,
                pap.display_name as Label,
                coalesce(pap.description, '') as Description,
                count(rcm.id)::int as MappingCount
            from package_area_profiles pap
            left join region_clusters rc on rc.code = pap.cluster_code
            left join region_cluster_mappings rcm on rcm.cluster_id = rc.id
            where pap.is_active = true
            group by pap.cluster_code, pap.display_name, pap.description, pap.sort_order
            order by pap.sort_order, pap.display_name;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AdminAreaMappingResponse>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private async Task<IReadOnlyList<AdminPackageSettingResponse>> GetPackageSettingsAsync(CancellationToken cancellationToken)
    {
        var rows = await (from band in _db.PackageBands.AsNoTracking()
                          join profile in _db.PackageBandProfiles.AsNoTracking() on band.Id equals profile.PackageBandId into profileJoin
                          from profile in profileJoin.DefaultIfEmpty()
                          orderby band.SortOrder, band.Name
                          select new
                          {
                              band.Id,
                              band.Code,
                              band.Name,
                              band.MinBudget,
                              band.MaxBudget,
                              band.SortOrder,
                              band.IsActive,
                              Description = profile != null ? profile.Description : string.Empty,
                              AudienceFit = profile != null ? profile.AudienceFit : string.Empty,
                              QuickBenefit = profile != null ? profile.QuickBenefit : string.Empty,
                              PackagePurpose = profile != null ? profile.PackagePurpose : string.Empty,
                              IncludeRadio = profile != null ? profile.IncludeRadio : "optional",
                              IncludeTv = profile != null ? profile.IncludeTv : "no",
                              LeadTime = profile != null ? profile.LeadTimeLabel : string.Empty,
                              RecommendedSpend = profile != null ? profile.RecommendedSpend : null,
                              IsRecommended = profile != null && profile.IsRecommended,
                              BenefitsJson = profile != null ? profile.BenefitsJson : "[]"
                          })
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => new AdminPackageSettingResponse
        {
            Id = row.Id,
            Code = row.Code,
            Name = row.Name,
            MinBudget = row.MinBudget,
            MaxBudget = row.MaxBudget,
            SortOrder = row.SortOrder,
            IsActive = row.IsActive,
            Description = row.Description,
            RecommendedSpend = row.RecommendedSpend,
            IsRecommended = row.IsRecommended,
            PackagePurpose = row.PackagePurpose,
            AudienceFit = row.AudienceFit,
            QuickBenefit = row.QuickBenefit,
            IncludeRadio = row.IncludeRadio,
            IncludeTv = row.IncludeTv,
            LeadTime = row.LeadTime,
            Benefits = DeserializeJsonList(row.BenefitsJson)
        }).ToArray();
    }

    private async Task<AdminPricingSettingsResponse> GetPricingSettingsAsync(CancellationToken cancellationToken)
    {
        var row = await _db.PricingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PricingKey == "default", cancellationToken);

        if (row is null)
        {
            return new AdminPricingSettingsResponse
            {
                AiStudioReservePercent = 0.10m,
                OohMarkupPercent = 0.05m,
                RadioMarkupPercent = 0.10m,
                TvMarkupPercent = 0.10m,
                DigitalMarkupPercent = 0.10m
            };
        }

        return new AdminPricingSettingsResponse
        {
            AiStudioReservePercent = row.AiStudioReservePercent,
            OohMarkupPercent = row.OohMarkupPercent,
            RadioMarkupPercent = row.RadioMarkupPercent,
            TvMarkupPercent = row.TvMarkupPercent,
            DigitalMarkupPercent = row.DigitalMarkupPercent
        };
    }

    private async Task<IReadOnlyList<AdminPreviewRuleResponse>> GetPreviewRulesAsync(CancellationToken cancellationToken)
    {
        var rows = await (from band in _db.PackageBands.AsNoTracking()
                          join tier in _db.PackageBandPreviewTiers.AsNoTracking() on band.Id equals tier.PackageBandId
                          where band.IsActive
                          orderby band.SortOrder, tier.TierCode
                          select new
                          {
                              band.Code,
                              band.Name,
                              tier.TierCode,
                              tier.TierLabel,
                              tier.TypicalInclusionsJson,
                              tier.IndicativeMixJson
                          })
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => new AdminPreviewRuleResponse
        {
            PackageCode = row.Code,
            PackageName = row.Name,
            TierCode = row.TierCode,
            TierLabel = row.TierLabel,
            TypicalInclusions = DeserializeJsonList(row.TypicalInclusionsJson),
            IndicativeMix = DeserializeJsonList(row.IndicativeMixJson)
        }).ToArray();
    }

    private async Task<IReadOnlyList<AdminUserResponse>> GetUsersAsync(IReadOnlyList<AdminAreaMappingResponse> areaMappings, CancellationToken cancellationToken)
    {
        var users = await _db.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToArrayAsync(cancellationToken);

        var assignments = await _db.AgentAreaAssignments
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var areaLabelsByCode = areaMappings.ToDictionary(x => x.Code, x => x.Label, StringComparer.OrdinalIgnoreCase);

        return users.Select(x => new AdminUserResponse
        {
            Id = x.Id,
            FullName = x.FullName,
            Email = x.Email,
            Phone = x.Phone,
            Role = x.Role switch
            {
                UserRole.CreativeDirector => "creative_director",
                UserRole.Agent => "agent",
                UserRole.Admin => "admin",
                _ => "client"
            },
            AccountStatus = x.AccountStatus.ToString(),
            IsSaCitizen = x.IsSaCitizen,
            EmailVerified = x.EmailVerified,
            PhoneVerified = x.PhoneVerified,
            AssignedAreaCodes = assignments.Where(assignment => assignment.AgentUserId == x.Id).Select(assignment => assignment.AreaCode).OrderBy(code => code).ToArray(),
            AssignedAreaLabels = assignments.Where(assignment => assignment.AgentUserId == x.Id).Select(assignment => areaLabelsByCode.TryGetValue(assignment.AreaCode, out var label) ? label : assignment.AreaCode).OrderBy(label => label).ToArray(),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToArray();
    }

    private async Task<IReadOnlyList<AdminAuditEntryResponse>> GetAuditEntriesAsync(CancellationToken cancellationToken)
    {
        var changeLogRows = await _db.ChangeAuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        var changeLogs = changeLogRows
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = FormatAuditSource(x.Scope),
                ActorName = string.IsNullOrWhiteSpace(x.ActorName) ? "System" : x.ActorName,
                ActorRole = x.ActorRole,
                EventType = x.Action,
                EntityType = x.EntityType,
                EntityLabel = x.EntityLabel,
                Context = x.Summary,
                StatusLabel = null,
                CreatedAt = x.CreatedAt
            })
            .ToArray();

        var requestRows = await _db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        var requestLogs = requestRows
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment request",
                ActorName = "System",
                ActorRole = "integration",
                EventType = x.EventType,
                EntityType = "package_order",
                EntityLabel = x.ExternalReference,
                Context = x.RequestUrl,
                StatusLabel = x.ResponseStatusCode?.ToString(),
                CreatedAt = x.CreatedAt
            })
            .ToArray();

        var webhookRows = await _db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        var webhookLogs = webhookRows
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment webhook",
                ActorName = "System",
                ActorRole = "integration",
                EventType = x.ProcessedStatus,
                EntityType = "package_order",
                EntityLabel = x.PackageOrderId.HasValue ? x.PackageOrderId.Value.ToString() : null,
                Context = x.WebhookPath,
                StatusLabel = x.ProcessedStatus,
                CreatedAt = x.CreatedAt
            })
            .ToArray();

        return changeLogs.Concat(requestLogs).Concat(webhookLogs)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToArray();
    }

    private static string FormatAuditSource(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return "System change";
        }

        var normalizedScope = scope.Trim().ToLowerInvariant();
        return $"{char.ToUpperInvariant(normalizedScope[0])}{normalizedScope.Substring(1)} change";
    }

    private async Task<AdminIntegrationStatusResponse> GetIntegrationsAsync(CancellationToken cancellationToken)
    {
        var requestCount = await _db.PaymentProviderRequests.CountAsync(cancellationToken);
        var webhookCount = await _db.PaymentProviderWebhooks.CountAsync(cancellationToken);
        var lastRequestAt = await _db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
            .Select(x => (DateTime?)(x.CompletedAt ?? x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        var lastWebhookAt = await _db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminIntegrationStatusResponse
        {
            PaymentRequestAuditCount = requestCount,
            PaymentWebhookAuditCount = webhookCount,
            LastPaymentRequestAt = lastRequestAt,
            LastPaymentWebhookAt = lastWebhookAt
        };
    }

    private async Task<AdminMonitoringResponse> GetMonitoringAsync(int activeAreaCount, CancellationToken cancellationToken)
    {
        const int retryAlertThreshold = 3;
        const int unpaidBacklogDays = 7;
        const int staleProspectDays = 7;

        var totalCampaigns = await _db.Campaigns.CountAsync(cancellationToken);
        var planningReadyCount = await _db.Campaigns.CountAsync(
            x => x.Status == CampaignStatuses.BriefSubmitted || x.Status == CampaignStatuses.PlanningInProgress,
            cancellationToken);
        var waitingOnClientCount = await _db.CampaignRecommendations.CountAsync(x => x.Status == RecommendationStatuses.SentToClient, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var inventoryRows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from inventory_items_final",
            cancellationToken: cancellationToken));
        var recommendationCount = await _db.CampaignRecommendations.CountAsync(cancellationToken);
        var creativeAlertCount = await _db.AiCreativeJobStatuses
            .AsNoTracking()
            .CountAsync(x => x.Status == "failed" && x.RetryAttemptCount >= retryAlertThreshold, cancellationToken);
        var assetAlertCount = await _db.AiAssetJobs
            .AsNoTracking()
            .CountAsync(x => x.Status == "failed" && x.RetryAttemptCount >= retryAlertThreshold, cancellationToken);
        var aiCostCapRejectionCount = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(x => x.Status == "rejected", cancellationToken);
        var creativeQueueBacklogCount = await _db.AiCreativeJobStatuses
            .AsNoTracking()
            .CountAsync(x => x.Status == "queued" || x.Status == "running" || x.Status == "retrying", cancellationToken);
        var assetQueueBacklogCount = await _db.AiAssetJobs
            .AsNoTracking()
            .CountAsync(x => x.Status == "queued" || x.Status == "running" || x.Status == "retrying", cancellationToken);
        var creativeDeadLetterCount = await _db.AiCreativeJobDeadLetters
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var publishSuccessCount = await _db.AiAdVariants
            .AsNoTracking()
            .CountAsync(x => x.Status == "published" || x.Status == "promoted", cancellationToken);
        var publishFailureCount = await _db.AiAdVariants
            .AsNoTracking()
            .CountAsync(x => x.Status == "publish_failed", cancellationToken);
        var latestMetricsRecordedAt = await _db.AiAdMetrics
            .AsNoTracking()
            .OrderByDescending(x => x.RecordedAt)
            .Select(x => (DateTime?)x.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var metricsSyncLagMinutes = latestMetricsRecordedAt.HasValue
            ? (int)Math.Max(0, Math.Round((DateTime.UtcNow - latestMetricsRecordedAt.Value).TotalMinutes))
            : 0;
        var creativeAlerts = await _db.AiCreativeJobStatuses
            .AsNoTracking()
            .Where(x => x.Status == "failed" && x.RetryAttemptCount >= retryAlertThreshold)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(20)
            .Select(x => new AdminAiJobAlertResponse
            {
                Pipeline = "creative",
                JobId = x.JobId,
                CampaignId = x.CampaignId,
                Status = x.Status,
                RetryAttemptCount = x.RetryAttemptCount,
                LastFailure = x.LastFailure ?? x.Error,
                UpdatedAt = x.UpdatedAt,
                AlertReason = "Status failed with repeated retries."
            })
            .ToArrayAsync(cancellationToken);
        var assetAlerts = await _db.AiAssetJobs
            .AsNoTracking()
            .Where(x => x.Status == "failed" && x.RetryAttemptCount >= retryAlertThreshold)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(20)
            .Select(x => new AdminAiJobAlertResponse
            {
                Pipeline = "asset",
                JobId = x.Id,
                CampaignId = x.CampaignId,
                Status = x.Status,
                RetryAttemptCount = x.RetryAttemptCount,
                LastFailure = x.LastFailure ?? x.Error,
                UpdatedAt = x.UpdatedAt,
                AlertReason = "Status failed with repeated retries."
            })
            .ToArrayAsync(cancellationToken);
        var aiJobAlerts = creativeAlerts
            .Concat(assetAlerts)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(20)
            .ToArray();
        var lifecycleQueues = await BuildLifecycleQueuesAsync(unpaidBacklogDays, staleProspectDays, cancellationToken);

        return new AdminMonitoringResponse
        {
            TotalCampaigns = totalCampaigns,
            PlanningReadyCount = planningReadyCount,
            WaitingOnClientCount = waitingOnClientCount,
            InventoryRows = inventoryRows,
            ActiveAreaCount = activeAreaCount,
            RecommendationCount = recommendationCount,
            RetryAlertThreshold = retryAlertThreshold,
            AiCreativeJobAlertCount = creativeAlertCount,
            AiAssetJobAlertCount = assetAlertCount,
            AiJobAlertCount = creativeAlertCount + assetAlertCount,
            AiCostCapRejectionCount = aiCostCapRejectionCount,
            CreativeQueueBacklogCount = creativeQueueBacklogCount,
            AssetQueueBacklogCount = assetQueueBacklogCount,
            CreativeDeadLetterCount = creativeDeadLetterCount,
            PublishSuccessCount = publishSuccessCount,
            PublishFailureCount = publishFailureCount,
            MetricsSyncLagMinutes = metricsSyncLagMinutes,
            UnpaidOrderBacklogCount = lifecycleQueues.FirstOrDefault(x => x.QueueKey == "unpaid_orders")?.Count ?? 0,
            UnsentProposalBacklogCount = lifecycleQueues.FirstOrDefault(x => x.QueueKey == "unsent_proposals")?.Count ?? 0,
            UnopenedProposalBacklogCount = lifecycleQueues.FirstOrDefault(x => x.QueueKey == "unopened_proposals")?.Count ?? 0,
            PaidActivationBacklogCount = lifecycleQueues.FirstOrDefault(x => x.QueueKey == "paid_activation_backlog")?.Count ?? 0,
            StaleProspectBacklogCount = lifecycleQueues.FirstOrDefault(x => x.QueueKey == "stale_prospects")?.Count ?? 0,
            AiJobAlerts = aiJobAlerts,
            LifecycleQueues = lifecycleQueues
        };
    }

    private async Task<IReadOnlyList<AdminLifecycleQueueItemResponse>> BuildLifecycleQueuesAsync(
        int unpaidBacklogDays,
        int staleProspectDays,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.ProspectLead)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .Include(x => x.EmailDeliveryMessages)
            .ToArrayAsync(cancellationToken);

        var queueItems = campaigns
            .Select(campaign => BuildLifecycleQueueCandidate(campaign, now))
            .ToArray();

        return new[]
        {
            BuildLifecycleQueue(
                "unpaid_orders",
                "Unpaid Orders Older Than 7 Days",
                "Orders still awaiting payment after 7 days.",
                queueItems.Where(item => item.UnpaidOrderAgeDays >= unpaidBacklogDays)),
            BuildLifecycleQueue(
                "unsent_proposals",
                "Generated Proposals Never Sent",
                "Campaigns with proposals ready to send but no outbound send recorded.",
                queueItems.Where(item => item.Lifecycle.CurrentState == "ready_to_send")),
            BuildLifecycleQueue(
                "unopened_proposals",
                "Sent Proposals Never Opened",
                "Proposal emails were sent or delivered, but no open or click has been recorded yet.",
                queueItems.Where(item => item.Lifecycle.CurrentState is "sent" or "delivered")),
            BuildLifecycleQueue(
                "paid_activation_backlog",
                "Paid Campaigns Not Progressed To Activation",
                "Campaigns have payment cleared but are still waiting to move into activation readiness.",
                queueItems.Where(item => item.Lifecycle.CurrentState == "paid")),
            BuildLifecycleQueue(
                "stale_prospects",
                "Prospects With No Recent Activity",
                "Prospect opportunities with no meaningful activity in the last 7 days.",
                queueItems.Where(item => item.Lifecycle.CommercialState == "prospect" && item.LastActivityAgeDays >= staleProspectDays))
        };
    }

    private static AdminLifecycleQueueItemResponse BuildLifecycleQueue(
        string queueKey,
        string label,
        string description,
        IEnumerable<LifecycleQueueCandidate> matches)
    {
        var materializedMatches = matches.ToArray();
        var items = materializedMatches
            .OrderByDescending(item => item.AgeDays)
            .ThenBy(item => item.CampaignName, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(item => new AdminLifecycleQueueCampaignResponse
            {
                CampaignId = item.CampaignId,
                CampaignName = item.CampaignName,
                ClientName = item.ClientName,
                CurrentState = item.Lifecycle.CurrentState,
                CommercialState = item.Lifecycle.CommercialState,
                PaymentState = item.Lifecycle.PaymentState,
                CommunicationState = item.Lifecycle.CommunicationState,
                AgeDays = item.AgeDays,
                LastActivityAt = item.LastActivityAt
            })
            .ToArray();

        return new AdminLifecycleQueueItemResponse
        {
            QueueKey = queueKey,
            Label = label,
            Description = description,
            Count = materializedMatches.Length,
            Items = items
        };
    }

    private static LifecycleQueueCandidate BuildLifecycleQueueCandidate(Data.Entities.Campaign campaign, DateTime now)
    {
        var lifecycle = CampaignLifecycleSupport.Build(campaign);
        var lastActivityAt = CampaignLifecycleSupport.ResolveLastActivityAt(campaign);
        var unpaidOrderAgeDays = lifecycle.PaymentState == "payment_pending" && !PackageOrderIntentPolicy.IsProspect(campaign.PackageOrder)
            ? (int)Math.Max(0, Math.Floor((now - campaign.PackageOrder.CreatedAt).TotalDays))
            : 0;
        var ageAnchor = lifecycle.CurrentState switch
        {
            "ready_to_send" => campaign.CampaignRecommendations.OrderByDescending(x => x.UpdatedAt).Select(x => x.UpdatedAt).FirstOrDefault(),
            "sent" or "delivered" => campaign.EmailDeliveryMessages
                .Where(x => string.Equals(x.DeliveryPurpose, "recommendation_ready", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.CreatedAt)
                .FirstOrDefault(),
            "paid" => campaign.PackageOrder.UpdatedAt,
            _ => lastActivityAt
        };
        if (ageAnchor == default)
        {
            ageAnchor = lastActivityAt;
        }

        return new LifecycleQueueCandidate(
            campaign.Id,
            ResolveCampaignLabel(campaign),
            campaign.ResolveClientName(),
            lifecycle,
            lastActivityAt,
            Math.Max(0, (int)Math.Floor((now - lastActivityAt).TotalDays)),
            Math.Max(0, (int)Math.Floor((now - ageAnchor).TotalDays)),
            unpaidOrderAgeDays);
    }

    private async Task<int> GetFallbackRatePercentAsync(CancellationToken cancellationToken)
    {
        const string marker = "Fallback flags:";
        var totalLatestRecommendations = await _db.CampaignRecommendations
            .GroupBy(x => x.CampaignId)
            .Select(group => group.OrderByDescending(x => x.RevisionNumber).ThenByDescending(x => x.CreatedAt).First())
            .CountAsync(cancellationToken);

        if (totalLatestRecommendations == 0)
        {
            return 0;
        }

        var latestRecommendations = await _db.CampaignRecommendations
            .AsNoTracking()
            .GroupBy(x => x.CampaignId)
            .Select(group => group.OrderByDescending(x => x.RevisionNumber).ThenByDescending(x => x.CreatedAt).First())
            .ToArrayAsync(cancellationToken);

        var fallbackCount = latestRecommendations.Count(x =>
            !string.IsNullOrWhiteSpace(x.Rationale) &&
            x.Rationale.Contains(marker, StringComparison.OrdinalIgnoreCase));

        return (int)Math.Round((decimal)fallbackCount / totalLatestRecommendations * 100m, MidpointRounding.AwayFromZero);
    }

    private AdminEnginePolicyResponse[] BuildEnginePolicies()
    {
        var planningPolicyOptions = _planningPolicySnapshotProvider.GetCurrent();
        return new[]
        {
            new AdminEnginePolicyResponse
            {
                PackageCode = "scale",
                BudgetFloor = planningPolicyOptions.Scale.BudgetFloor,
                MinimumNationalRadioCandidates = planningPolicyOptions.Scale.MinimumNationalRadioCandidates,
                RequireNationalCapableRadio = planningPolicyOptions.Scale.RequireNationalCapableRadio,
                RequirePremiumNationalRadio = planningPolicyOptions.Scale.RequirePremiumNationalRadio,
                NationalRadioBonus = planningPolicyOptions.Scale.NationalRadioBonus,
                NonNationalRadioPenalty = planningPolicyOptions.Scale.NonNationalRadioPenalty,
                RegionalRadioPenalty = planningPolicyOptions.Scale.RegionalRadioPenalty
            },
            new AdminEnginePolicyResponse
            {
                PackageCode = "dominance",
                BudgetFloor = planningPolicyOptions.Dominance.BudgetFloor,
                MinimumNationalRadioCandidates = planningPolicyOptions.Dominance.MinimumNationalRadioCandidates,
                RequireNationalCapableRadio = planningPolicyOptions.Dominance.RequireNationalCapableRadio,
                RequirePremiumNationalRadio = planningPolicyOptions.Dominance.RequirePremiumNationalRadio,
                NationalRadioBonus = planningPolicyOptions.Dominance.NationalRadioBonus,
                NonNationalRadioPenalty = planningPolicyOptions.Dominance.NonNationalRadioPenalty,
                RegionalRadioPenalty = planningPolicyOptions.Dominance.RegionalRadioPenalty
            }
        };
    }

    private AdminPlanningAllocationSettingsResponse BuildPlanningAllocationSettings()
    {
        var snapshot = _planningBudgetAllocationSnapshotProvider.GetCurrent();
        return new AdminPlanningAllocationSettingsResponse
        {
            BudgetBands = snapshot.BudgetBands
                .OrderBy(band => band.Min)
                .ThenBy(band => band.Max)
                .Select(band => new AdminPlanningBudgetBandResponse
                {
                    Name = band.Name,
                    Min = band.Min,
                    Max = band.Max,
                    OohTarget = band.OohTarget,
                    BillboardShareOfOoh = band.BillboardShareOfOoh,
                    TvMin = band.TvMin,
                    TvEligible = band.TvEligible,
                    RadioRange = band.RadioRange.ToArray(),
                    DigitalRange = band.DigitalRange.ToArray()
                })
                .ToArray(),
            GlobalRules = new AdminPlanningAllocationGlobalRulesResponse
            {
                MaxOoh = snapshot.GlobalRules.MaxOoh,
                MinDigital = snapshot.GlobalRules.MinDigital,
                EnforceTvFloorIfPreferred = snapshot.GlobalRules.EnforceTvFloorIfPreferred
            }
        };
    }

    private async Task<AdminLeadIndustryPolicyResponse[]> GetLeadIndustryPoliciesAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.LeadIndustryPolicySettings
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Key)
            .ToArrayAsync(cancellationToken);

        return rows.Select(policy => new AdminLeadIndustryPolicyResponse
            {
                Key = policy.Key,
                Name = policy.Name,
                ObjectiveOverride = policy.ObjectiveOverride,
                PreferredTone = policy.PreferredTone,
                PreferredChannels = DeserializeJsonList(policy.PreferredChannelsJson),
                Cta = policy.Cta,
                MessagingAngle = policy.MessagingAngle,
                Guardrails = DeserializeJsonList(policy.GuardrailsJson),
                AdditionalGap = policy.AdditionalGap,
                AdditionalOutcome = policy.AdditionalOutcome,
                SortOrder = policy.SortOrder,
                IsActive = policy.IsActive
            })
            .ToArray();
    }

    private static AdminOutletResponse MapOutlet(BroadcastInventoryRecord record)
    {
        var packagePrices = ExtractNumericValues(record.Packages, "investment_zar", "package_cost_zar", "cost_per_month_zar");
        var slotRates = ExtractRateValues(record.Pricing);
        var normalizedHealth = DetermineHealthBucket(record);

        return new AdminOutletResponse
        {
            Code = record.Id,
            Name = record.Station,
            MediaType = record.MediaType,
            CoverageType = record.CoverageType,
            GeographyLabel = BuildGeographyLabel(record),
            CatalogHealth = normalizedHealth,
            HasPricing = record.HasPricing,
            PackageCount = record.Packages.ValueKind == JsonValueKind.Array ? record.Packages.GetArrayLength() : 0,
            SlotRateCount = slotRates.Count,
            MinPackagePrice = packagePrices.Count > 0 ? packagePrices.Min() : null,
            MinSlotRate = slotRates.Count > 0 ? slotRates.Min() : null,
            LanguageDisplay = record.LanguageDisplay,
            BroadcastFrequency = record.BroadcastFrequency
        };
    }

    private static AdminHealthIssueResponse? MapHealthIssue(BroadcastInventoryRecord record, AdminOutletMasterDataResponse masterData)
    {
        var normalizedHealth = DetermineHealthBucket(record);
        var hasGeography = record.ProvinceCodes.Count > 0 || record.CityLabels.Count > 0;
        var validLanguageCodes = masterData.Languages.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validProvinceCodes = masterData.Provinces.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validCoverageTypes = masterData.CoverageTypes.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validCatalogHealthStates = masterData.CatalogHealthStates.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidLanguages = record.PrimaryLanguages.Where(x => !validLanguageCodes.Contains(x)).ToArray();
        if (invalidLanguages.Length > 0)
        {
            return new AdminHealthIssueResponse
            {
                OutletCode = record.Id,
                OutletName = record.Station,
                Category = "master_data",
                Issue = "Invalid language code",
                Impact = $"Lookup constraints and filtering will drift for {string.Join(", ", invalidLanguages)}.",
                SuggestedFix = "Replace unsupported language codes with canonical reference data values."
            };
        }

        var invalidProvinces = record.ProvinceCodes.Where(x => !validProvinceCodes.Contains(x)).ToArray();
        if (invalidProvinces.Length > 0)
        {
            return new AdminHealthIssueResponse
            {
                OutletCode = record.Id,
                OutletName = record.Station,
                Category = "master_data",
                Issue = "Invalid province code",
                Impact = $"Geography matching will drift for {string.Join(", ", invalidProvinces)}.",
                SuggestedFix = "Replace unsupported province codes with canonical reference data values."
            };
        }

        if (!string.IsNullOrWhiteSpace(record.CoverageType) && !validCoverageTypes.Contains(record.CoverageType))
        {
            return new AdminHealthIssueResponse
            {
                OutletCode = record.Id,
                OutletName = record.Station,
                Category = "master_data",
                Issue = "Invalid coverage type",
                Impact = "Planner and admin filters will not classify this outlet correctly.",
                SuggestedFix = "Use a canonical coverage type from the broadcast master data set."
            };
        }

        if (!string.IsNullOrWhiteSpace(record.CatalogHealth) && !validCatalogHealthStates.Contains(record.CatalogHealth))
        {
            return new AdminHealthIssueResponse
            {
                OutletCode = record.Id,
                OutletName = record.Station,
                Category = "master_data",
                Issue = "Invalid health state",
                Impact = "Catalog health routing and admin queues will be inconsistent.",
                SuggestedFix = "Use a canonical catalog health state from the broadcast master data set."
            };
        }

        if (string.Equals(normalizedHealth, "strong", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var issue = normalizedHealth switch
        {
            "weak_unpriced" => "Missing pricing",
            "weak_no_inventory" => "No usable inventory",
            "mixed_not_fully_healthy" when !hasGeography => "Missing geography mapping",
            "mixed_not_fully_healthy" => "Inventory health needs review",
            _ => "Inventory health needs review"
        };

        var impact = normalizedHealth switch
        {
            "weak_unpriced" => "Preview and planner confidence is reduced.",
            "weak_no_inventory" => "Recommendations cannot use this outlet reliably.",
            _ when !hasGeography => "Area matching will be less reliable.",
            _ => "Recommendation quality is reduced for this outlet."
        };

        var suggestedFix = normalizedHealth switch
        {
            "weak_unpriced" => "Load package or slot pricing for this outlet.",
            "weak_no_inventory" => "Import usable package or slot inventory for this outlet.",
            _ when !hasGeography => "Add province and city mappings for the outlet.",
            _ => "Review metadata and pricing coverage."
        };

        var category = normalizedHealth switch
        {
            "weak_unpriced" => "pricing",
            "weak_no_inventory" => "inventory",
            _ when !hasGeography => "geography",
            _ => "review"
        };

        return new AdminHealthIssueResponse
        {
            OutletCode = record.Id,
            OutletName = record.Station,
            Category = category,
            Issue = issue,
            Impact = impact,
            SuggestedFix = suggestedFix
        };
    }

    private static string DetermineHealthBucket(BroadcastInventoryRecord record)
    {
        var rawHealth = (record.CatalogHealth ?? string.Empty).Trim().ToLowerInvariant();
        var hasPackages = record.Packages.ValueKind == JsonValueKind.Array && record.Packages.GetArrayLength() > 0;
        var hasSlotRates = ExtractRateValues(record.Pricing).Count > 0;
        var hasInventory = hasPackages || hasSlotRates;
        var hasGeography = record.ProvinceCodes.Count > 0 || record.CityLabels.Count > 0;

        if (!record.HasPricing)
        {
            return "weak_unpriced";
        }

        if (!hasInventory)
        {
            return "weak_no_inventory";
        }

        if (string.Equals(rawHealth, "strong", StringComparison.OrdinalIgnoreCase))
        {
            return "strong";
        }

        if (string.Equals(rawHealth, "weak_unpriced", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawHealth, "weak_no_inventory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawHealth, "mixed_not_fully_healthy", StringComparison.OrdinalIgnoreCase))
        {
            return rawHealth;
        }

        if (string.Equals(rawHealth, "mixed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawHealth, "unknown", StringComparison.OrdinalIgnoreCase) ||
            !hasGeography)
        {
            return "mixed_not_fully_healthy";
        }

        return "mixed_not_fully_healthy";
    }

    private static bool HasOutletIssue(AdminOutletResponse outlet)
    {
        return outlet.CatalogHealth != "strong"
            || !outlet.HasPricing
            || (outlet.PackageCount == 0 && outlet.SlotRateCount == 0)
            || string.IsNullOrWhiteSpace(outlet.LanguageDisplay);
    }

    private static IEnumerable<AdminOutletResponse> SortOutlets(IEnumerable<AdminOutletResponse> outlets, string sortBy)
    {
        return sortBy switch
        {
            "name" => outlets.OrderBy(static item => item.Name),
            "coverage" => outlets.OrderBy(static item => item.CoverageType).ThenBy(static item => item.Name),
            _ => outlets.OrderByDescending(PriorityScore).ThenBy(static item => item.Name)
        };
    }

    private static int PriorityScore(AdminOutletResponse item)
    {
        var score = item.CatalogHealth switch
        {
            "weak_no_inventory" => 100,
            "weak_unpriced" => 85,
            "mixed_not_fully_healthy" => 60,
            _ => 20
        };

        if (!item.HasPricing)
        {
            score += 25;
        }

        if (item.PackageCount == 0 && item.SlotRateCount == 0)
        {
            score += 20;
        }

        if (string.IsNullOrWhiteSpace(item.LanguageDisplay))
        {
            score += 8;
        }

        return score;
    }

    private static string NormalizeOutletSort(string? sortBy)
    {
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "name" => "name",
            "coverage" => "coverage",
            _ => "priority"
        };
    }

    private static string BuildGeographyLabel(BroadcastInventoryRecord record)
    {
        var cities = record.CityLabels.Take(2).ToArray();
        if (cities.Length > 0)
        {
            return string.Join(", ", cities);
        }

        var provinces = record.ProvinceCodes.Take(2).ToArray();
        if (provinces.Length > 0)
        {
            return string.Join(", ", provinces);
        }

        return record.CoverageType;
    }

    private static string ResolveCampaignLabel(Data.Entities.Campaign campaign)
    {
        if (!string.IsNullOrWhiteSpace(campaign.CampaignName))
        {
            return campaign.CampaignName.Trim();
        }

        return $"{campaign.PackageBand.Name} campaign";
    }

    private static List<decimal> ExtractNumericValues(JsonElement element, params string[] keys)
    {
        var values = new List<decimal>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var item in element.EnumerateArray())
        {
            foreach (var key in keys)
            {
                if (!item.TryGetProperty(key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numeric) && numeric > 0)
                {
                    values.Add(numeric);
                }
                else if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed) && parsed > 0)
                {
                    values.Add(parsed);
                }
            }
        }

        return values;
    }

    private static List<decimal> ExtractRateValues(JsonElement pricing)
    {
        var values = new List<decimal>();
        if (pricing.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in pricing.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var slot in group.Value.EnumerateObject())
                {
                    if (slot.Value.ValueKind == JsonValueKind.Number && slot.Value.TryGetDecimal(out var value) && value > 0)
                    {
                        values.Add(value);
                    }
                }
            }
        }
        else if (pricing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pricing.EnumerateArray())
            {
                if (item.TryGetProperty("price_zar", out var price)
                    && price.ValueKind == JsonValueKind.Number
                    && price.TryGetDecimal(out var priceValue)
                    && priceValue > 0)
                {
                    values.Add(priceValue);
                    continue;
                }

                if (item.TryGetProperty("rate_zar", out var rate)
                    && rate.ValueKind == JsonValueKind.Number
                    && rate.TryGetDecimal(out var rateValue)
                    && rateValue > 0)
                {
                    values.Add(rateValue);
                }
            }
        }

        return values;
    }

    private static IReadOnlyList<string> DeserializeJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        var items = JsonSerializer.Deserialize<List<string>>(json);
        return items ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private sealed record LifecycleQueueCandidate(
        Guid CampaignId,
        string CampaignName,
        string ClientName,
        Contracts.Campaigns.CampaignLifecycleResponse Lifecycle,
        DateTime LastActivityAt,
        int LastActivityAgeDays,
        int AgeDays,
        int UnpaidOrderAgeDays);
}

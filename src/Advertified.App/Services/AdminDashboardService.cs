using System.Text.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Advertified.App.Services;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly IPackageCatalogService _packageCatalogService;
    private readonly PlanningPolicyOptions _planningPolicyOptions;
    private readonly string _connectionString;

    public AdminDashboardService(
        AppDbContext db,
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IPackageCatalogService packageCatalogService,
        IOptions<PlanningPolicyOptions> planningPolicyOptions,
        string connectionString)
    {
        _db = db;
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _packageCatalogService = packageCatalogService;
        _planningPolicyOptions = planningPolicyOptions.Value;
        _connectionString = connectionString;
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var outletRecords = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        var outlets = outletRecords
            .Select(MapOutlet)
            .OrderBy(x => x.MediaType)
            .ThenBy(x => x.Name)
            .ToArray();

        var packageSettings = _packageCatalogService.GetPackageBands()
            .Select(x => new AdminPackageSettingResponse
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                MinBudget = x.MinBudget,
                MaxBudget = x.MaxBudget,
                RecommendedSpend = x.RecommendedSpend,
                PackagePurpose = x.PackagePurpose,
                AudienceFit = x.AudienceFit,
                QuickBenefit = x.QuickBenefit,
                IncludeRadio = x.IncludeRadio,
                IncludeTv = x.IncludeTv,
                LeadTime = x.LeadTime,
                Benefits = x.Benefits
            })
            .ToArray();

        var strongCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "strong", StringComparison.OrdinalIgnoreCase));
        var mixedCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "mixed_not_fully_healthy", StringComparison.OrdinalIgnoreCase));
        var weakUnpricedCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "weak_unpriced", StringComparison.OrdinalIgnoreCase));
        var weakNoInventoryCount = outletRecords.Count(x => string.Equals(DetermineHealthBucket(x), "weak_no_inventory", StringComparison.OrdinalIgnoreCase));
        var weakOutletCount = outletRecords.Count(x => !string.Equals(DetermineHealthBucket(x), "strong", StringComparison.OrdinalIgnoreCase));
        var weakOutlets = outletRecords
            .Where(x => !string.Equals(DetermineHealthBucket(x), "strong", StringComparison.OrdinalIgnoreCase))
            .Select(MapHealthIssue)
            .Take(10)
            .ToArray();

        var sourceDocuments = await GetSourceDocumentCountAsync(cancellationToken);
        var recentImports = await GetRecentImportsAsync(cancellationToken);
        var areaMappings = await GetAreaMappingsAsync(cancellationToken);
        var auditEntries = await GetAuditEntriesAsync(cancellationToken);
        var users = await GetUsersAsync(cancellationToken);
        var integrations = await GetIntegrationsAsync(cancellationToken);
        var previewRules = await GetPreviewRulesAsync(cancellationToken);
        var monitoring = await GetMonitoringAsync(areaMappings.Count, cancellationToken);
        var fallbackRatePercent = await GetFallbackRatePercentAsync(cancellationToken);

        return new AdminDashboardResponse
        {
            Summary = new AdminSummaryResponse
            {
                ActiveOutlets = outlets.Length,
                WeakOutlets = weakOutletCount,
                SourceDocuments = sourceDocuments,
                FallbackRatePercent = fallbackRatePercent
            },
            Alerts = weakOutlets.Take(3).Select(issue => new AdminAlertResponse
            {
                Title = issue.Issue,
                Context = $"{issue.OutletName} | {issue.Impact}",
                Severity = issue.Issue.Contains("Missing", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Warning"
            }).ToArray(),
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
            EnginePolicies = BuildEnginePolicies(),
            PreviewRules = previewRules,
            Monitoring = monitoring,
            Users = users,
            AuditEntries = auditEntries,
            Integrations = integrations
        };
    }

    private async Task<int> GetSourceDocumentCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
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
                im.page_count as PageCount,
                im.imported_at as ImportedAt
            from import_manifest im
            left join package_document_metadata pdm on pdm.source_file = im.source_file
            order by im.imported_at desc
            limit 12;";

        await using var connection = new NpgsqlConnection(_connectionString);
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

        await using var connection = new NpgsqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AdminAreaMappingResponse>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToArray();
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

    private async Task<IReadOnlyList<AdminUserResponse>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _db.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToArrayAsync(cancellationToken);

        return users.Select(x => new AdminUserResponse
        {
            Id = x.Id,
            FullName = x.FullName,
            Email = x.Email,
            Phone = x.Phone,
            Role = x.Role.ToString().ToLowerInvariant(),
            AccountStatus = x.AccountStatus.ToString(),
            IsSaCitizen = x.IsSaCitizen,
            EmailVerified = x.EmailVerified,
            PhoneVerified = x.PhoneVerified,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToArray();
    }

    private async Task<IReadOnlyList<AdminAuditEntryResponse>> GetAuditEntriesAsync(CancellationToken cancellationToken)
    {
        var requestLogs = await _db.PaymentProviderRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment request",
                Provider = x.Provider,
                EventType = x.EventType,
                ExternalReference = x.ExternalReference,
                RequestUrl = x.RequestUrl,
                ResponseStatusCode = x.ResponseStatusCode,
                CreatedAt = x.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        var webhookLogs = await _db.PaymentProviderWebhooks
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new AdminAuditEntryResponse
            {
                Id = x.Id,
                Source = "Payment webhook",
                Provider = x.Provider,
                EventType = x.ProcessedStatus,
                ExternalReference = x.PackageOrderId.HasValue ? x.PackageOrderId.Value.ToString() : null,
                RequestUrl = x.WebhookPath,
                ResponseStatusCode = null,
                CreatedAt = x.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        return requestLogs.Concat(webhookLogs)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToArray();
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
        var totalCampaigns = await _db.Campaigns.CountAsync(cancellationToken);
        var planningReadyCount = await _db.Campaigns.CountAsync(
            x => x.Status == "brief_submitted" || x.Status == "planning_in_progress",
            cancellationToken);
        var waitingOnClientCount = await _db.CampaignRecommendations.CountAsync(x => x.Status == "sent_to_client", cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        var inventoryRows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from inventory_items_final",
            cancellationToken: cancellationToken));
        var recommendationCount = await _db.CampaignRecommendations.CountAsync(cancellationToken);

        return new AdminMonitoringResponse
        {
            TotalCampaigns = totalCampaigns,
            PlanningReadyCount = planningReadyCount,
            WaitingOnClientCount = waitingOnClientCount,
            InventoryRows = inventoryRows,
            ActiveAreaCount = activeAreaCount,
            RecommendationCount = recommendationCount
        };
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
        return new[]
        {
            new AdminEnginePolicyResponse
            {
                PackageCode = "scale",
                BudgetFloor = _planningPolicyOptions.Scale.BudgetFloor,
                MinimumNationalRadioCandidates = _planningPolicyOptions.Scale.MinimumNationalRadioCandidates,
                RequireNationalCapableRadio = _planningPolicyOptions.Scale.RequireNationalCapableRadio,
                RequirePremiumNationalRadio = _planningPolicyOptions.Scale.RequirePremiumNationalRadio,
                NationalRadioBonus = _planningPolicyOptions.Scale.NationalRadioBonus,
                NonNationalRadioPenalty = _planningPolicyOptions.Scale.NonNationalRadioPenalty,
                RegionalRadioPenalty = _planningPolicyOptions.Scale.RegionalRadioPenalty
            },
            new AdminEnginePolicyResponse
            {
                PackageCode = "dominance",
                BudgetFloor = _planningPolicyOptions.Dominance.BudgetFloor,
                MinimumNationalRadioCandidates = _planningPolicyOptions.Dominance.MinimumNationalRadioCandidates,
                RequireNationalCapableRadio = _planningPolicyOptions.Dominance.RequireNationalCapableRadio,
                RequirePremiumNationalRadio = _planningPolicyOptions.Dominance.RequirePremiumNationalRadio,
                NationalRadioBonus = _planningPolicyOptions.Dominance.NationalRadioBonus,
                NonNationalRadioPenalty = _planningPolicyOptions.Dominance.NonNationalRadioPenalty,
                RegionalRadioPenalty = _planningPolicyOptions.Dominance.RegionalRadioPenalty
            }
        };
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

    private static AdminHealthIssueResponse MapHealthIssue(BroadcastInventoryRecord record)
    {
        var normalizedHealth = DetermineHealthBucket(record);
        var hasGeography = record.ProvinceCodes.Count > 0 || record.CityLabels.Count > 0;
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

        return new AdminHealthIssueResponse
        {
            OutletCode = record.Id,
            OutletName = record.Station,
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
            return hasGeography ? "strong" : "mixed_not_fully_healthy";
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
}

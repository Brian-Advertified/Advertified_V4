using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class DbAdVariantService : IAdVariantService
{
    private readonly AppDbContext _db;
    private readonly IAdPlatformPublisherFactory _publisherFactory;
    private readonly IAiCostEstimator _costEstimator;
    private readonly IAiCostControlService _costControlService;
    private readonly IAdPlatformAccessTokenService _accessTokenService;
    private readonly ICampaignPerformanceProjectionService _campaignPerformanceProjectionService;
    private readonly ILogger<DbAdVariantService> _logger;

    public DbAdVariantService(
        AppDbContext db,
        IAdPlatformPublisherFactory publisherFactory,
        IAiCostEstimator costEstimator,
        IAiCostControlService costControlService,
        IAdPlatformAccessTokenService accessTokenService,
        ICampaignPerformanceProjectionService campaignPerformanceProjectionService,
        ILogger<DbAdVariantService> logger)
    {
        _db = db;
        _publisherFactory = publisherFactory;
        _costEstimator = costEstimator;
        _costControlService = costControlService;
        _accessTokenService = accessTokenService;
        _campaignPerformanceProjectionService = campaignPerformanceProjectionService;
        _logger = logger;
    }

    public async Task<AdVariantSummary> CreateVariantAsync(CreateAdVariantCommand command, CancellationToken cancellationToken)
    {
        if (command.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Script))
        {
            throw new InvalidOperationException("script is required.");
        }

        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(item => item.Id == command.CampaignId, cancellationToken);
        if (!campaignExists)
        {
            throw new InvalidOperationException("Campaign not found.");
        }

        var entitlement = await GetAdEntitlementAsync(command.CampaignId, cancellationToken);
        var normalizedPlatform = NormalizePlatform(command.Platform);
        EnsurePlatformAllowed(entitlement, normalizedPlatform);

        var variantCount = await _db.AiAdVariants
            .AsNoTracking()
            .CountAsync(item => item.CampaignId == command.CampaignId, cancellationToken);
        if (variantCount >= entitlement.MaxAdVariants)
        {
            throw new InvalidOperationException(
                $"Package limit reached. This package allows up to {entitlement.MaxAdVariants} ad variant(s).");
        }

        if (command.VoicePackId.HasValue)
        {
            var selectedVoiceTier = await _db.AiVoicePacks
                .AsNoTracking()
                .Where(item => item.Id == command.VoicePackId.Value && item.IsActive)
                .Select(item => item.PricingTier)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(selectedVoiceTier))
            {
                throw new InvalidOperationException("Selected voice pack is not available.");
            }

            EnsureVoiceTierAllowed(entitlement, selectedVoiceTier);
        }

        var now = DateTime.UtcNow;
        var row = new AiAdVariant
        {
            Id = Guid.NewGuid(),
            CampaignId = command.CampaignId,
            CampaignCreativeId = command.CampaignCreativeId,
            Platform = normalizedPlatform,
            Channel = NormalizeText(command.Channel, "Digital"),
            Language = NormalizeText(command.Language, "English"),
            TemplateId = command.TemplateId,
            VoicePackId = command.VoicePackId,
            VoicePackName = NormalizeOptionalText(command.VoicePackName),
            Script = command.Script.Trim(),
            AudioAssetUrl = NormalizeOptionalText(command.AudioAssetUrl),
            Status = "draft",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.AiAdVariants.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return MapVariant(row);
    }

    public async Task<IReadOnlyList<AdVariantSummary>> GetCampaignVariantsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var rows = await _db.AiAdVariants
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return rows.Select(MapVariant).ToArray();
    }

    public async Task<PublishAdVariantResult> PublishVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var row = await _db.AiAdVariants
            .FirstOrDefaultAsync(item => item.Id == variantId, cancellationToken)
            ?? throw new InvalidOperationException("Ad variant not found.");

        var entitlement = await GetAdEntitlementAsync(row.CampaignId, cancellationToken);
        EnsurePlatformAllowed(entitlement, row.Platform);

        if (!string.IsNullOrWhiteSpace(row.PlatformAdId) && string.Equals(row.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            return new PublishAdVariantResult(
                row.Id,
                row.CampaignId,
                row.Platform,
                row.PlatformAdId,
                row.Status,
                new DateTimeOffset(row.PublishedAt ?? row.UpdatedAt, TimeSpan.Zero));
        }

        var guard = await GuardAdOpsCostAsync(
            row.CampaignId,
            row.Id,
            operation: "ad_variant_publish",
            provider: row.Platform,
            estimatedCostZar: _costEstimator.EstimateQaCost(1),
            cancellationToken);

        var publisher = _publisherFactory.GetRequired(row.Platform);
        var linkedPublishConnection = await GetActiveConnectionForCampaignProviderAsync(row.CampaignId, row.Platform, cancellationToken);
        var publishToken = await _accessTokenService.ResolveAccessTokenAsync(linkedPublishConnection, row.Platform, cancellationToken);
        var summary = MapVariant(row);
        string platformAdId;
        try
        {
            platformAdId = await publisher.PublishAsync(summary, cancellationToken, publishToken);
        }
        catch (Exception ex)
        {
            row.Status = "publish_failed";
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            if (guard.UsageLogId.HasValue)
            {
                await _costControlService.FailAsync(guard.UsageLogId.Value, $"Publish failed: {ex.Message}", cancellationToken);
            }

            _logger.LogError(ex, "Ad publish failed for variant {VariantId} on {Platform}.", row.Id, row.Platform);
            throw;
        }

        var now = DateTime.UtcNow;
        row.PlatformAdId = platformAdId;
        row.Status = "published";
        row.PublishedAt = now;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        if (guard.UsageLogId.HasValue)
        {
            try
            {
                await _costControlService.CompleteAsync(guard.UsageLogId.Value, _costEstimator.EstimateQaCost(1), "Ad publish completed.", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete cost record {UsageLogId} for campaign {CampaignId}, ad variant {VariantId}.", guard.UsageLogId.Value, row.CampaignId, row.Id);
            }
        }

        _logger.LogInformation(
            "Ad variant {VariantId} published for campaign {CampaignId} on {Platform}.",
            row.Id,
            row.CampaignId,
            row.Platform);

        return new PublishAdVariantResult(
            row.Id,
            row.CampaignId,
            row.Platform,
            platformAdId,
            row.Status,
            new DateTimeOffset(now, TimeSpan.Zero));
    }

    public async Task RecordConversionAsync(Guid variantId, int conversions, CancellationToken cancellationToken)
    {
        if (conversions <= 0)
        {
            throw new InvalidOperationException("conversions must be greater than zero.");
        }

        var variant = await _db.AiAdVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == variantId, cancellationToken)
            ?? throw new InvalidOperationException("Ad variant not found.");

        var now = DateTime.UtcNow;
        _db.AiAdMetrics.Add(new AiAdMetric
        {
            Id = Guid.NewGuid(),
            CampaignId = variant.CampaignId,
            AdVariantId = variant.Id,
            Platform = variant.Platform,
            Source = "server_event",
            Impressions = 0,
            Clicks = 0,
            Conversions = conversions,
            CostZar = 0m,
            Ctr = 0m,
            ConversionRate = 0m,
            RecordedAt = now,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CampaignAdMetricsSummary> GetCampaignMetricsSummaryAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var variantStats = await _db.AiAdVariants
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                VariantCount = group.Count(),
                PublishedCount = group.Count(item => item.Status == "published")
            })
            .FirstOrDefaultAsync(cancellationToken);

        var metricRows = await _db.AiAdMetrics
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);

        var impressions = metricRows.Sum(item => item.Impressions);
        var clicks = metricRows.Sum(item => item.Clicks);
        var conversions = metricRows.Sum(item => item.Conversions);
        var cost = metricRows.Sum(item => item.CostZar);
        var ctr = impressions > 0 ? decimal.Round((decimal)clicks / impressions, 4) : 0m;
        var conversionRate = clicks > 0 ? decimal.Round((decimal)conversions / clicks, 4) : 0m;
        var lastRecordedAt = metricRows.Length > 0
            ? new DateTimeOffset(metricRows.Max(item => item.RecordedAt), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        var byVariant = metricRows
            .GroupBy(item => item.AdVariantId)
            .Select(group =>
            {
                var groupClicks = group.Sum(item => item.Clicks);
                var groupConversions = group.Sum(item => item.Conversions);
                var groupRate = groupClicks > 0 ? decimal.Round((decimal)groupConversions / groupClicks, 4) : 0m;
                return new { VariantId = group.Key, ConversionRate = groupRate };
            })
            .OrderByDescending(item => item.ConversionRate)
            .FirstOrDefault();

        return new CampaignAdMetricsSummary(
            campaignId,
            variantStats?.VariantCount ?? 0,
            variantStats?.PublishedCount ?? 0,
            impressions,
            clicks,
            conversions,
            cost,
            ctr,
            conversionRate,
            byVariant?.VariantId,
            byVariant?.ConversionRate,
            lastRecordedAt);
    }

    public async Task<SyncCampaignMetricsResult> SyncCampaignMetricsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var entitlement = await GetAdEntitlementAsync(campaignId, cancellationToken);
        if (!entitlement.AllowAdMetricsSync)
        {
            throw new InvalidOperationException("Your package does not include ad metrics sync.");
        }

        var publishedRows = await _db.AiAdVariants
            .Where(item =>
                item.CampaignId == campaignId
                && item.Status == "published"
                && item.PlatformAdId != null)
            .ToArrayAsync(cancellationToken);

        var guard = await GuardAdOpsCostAsync(
            campaignId,
            creativeId: null,
            operation: "ad_metrics_sync",
            provider: "AdPlatforms",
            estimatedCostZar: _costEstimator.EstimateQaCost(Math.Max(1, publishedRows.Length)),
            cancellationToken);

        var linkedConnections = await _db.CampaignAdPlatformLinks
            .Where(item =>
                item.CampaignId == campaignId
                && item.Status == "active"
                && item.AdPlatformConnection.Status == "active")
            .Include(item => item.AdPlatformConnection)
            .ToArrayAsync(cancellationToken);
        var linkedByProvider = linkedConnections
            .GroupBy(item => NormalizeProviderKey(item.AdPlatformConnection.Provider))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenByDescending(item => item.UpdatedAt)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var metricsByPlatform = new Dictionary<string, ExternalAdMetrics>(StringComparer.OrdinalIgnoreCase);
        var accountNameByPlatform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedConnectionIds = new HashSet<Guid>();
        foreach (var row in publishedRows)
        {
            var publisher = _publisherFactory.GetRequired(row.Platform);
            var providerKey = NormalizeProviderKey(row.Platform);
            linkedByProvider.TryGetValue(providerKey, out var linkedConnection);
            var syncedToken = await _accessTokenService.ResolveAccessTokenAsync(linkedConnection, row.Platform, cancellationToken);
            var external = await publisher.GetMetricsAsync(row.PlatformAdId!, cancellationToken, syncedToken);
            var ctr = external.Impressions > 0 ? decimal.Round((decimal)external.Clicks / external.Impressions, 4) : 0m;
            var conversionRate = external.Clicks > 0 ? decimal.Round((decimal)external.Conversions / external.Clicks, 4) : 0m;
            metricsByPlatform[providerKey] = MergeMetrics(metricsByPlatform.GetValueOrDefault(providerKey), external);
            if (linkedConnection is not null)
            {
                if (!string.IsNullOrWhiteSpace(linkedConnection.AdPlatformConnection.AccountName))
                {
                    accountNameByPlatform[providerKey] = linkedConnection.AdPlatformConnection.AccountName;
                }

                usedConnectionIds.Add(linkedConnection.AdPlatformConnectionId);
            }

            _db.AiAdMetrics.Add(new AiAdMetric
            {
                Id = Guid.NewGuid(),
                CampaignId = row.CampaignId,
                AdVariantId = row.Id,
                Platform = row.Platform,
                Source = "sync",
                Impressions = external.Impressions,
                Clicks = external.Clicks,
                Conversions = external.Conversions,
                CostZar = external.CostZar,
                Ctr = ctr,
                ConversionRate = conversionRate,
                RecordedAt = now,
                CreatedAt = now
            });
        }

        try
        {
            if (publishedRows.Length > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);

                foreach (var (platform, metrics) in metricsByPlatform)
                {
                    await _campaignPerformanceProjectionService.UpsertAdPlatformMetricsAsync(
                        campaignId,
                        platform,
                        metrics,
                        now,
                        accountNameByPlatform.GetValueOrDefault(platform),
                        cancellationToken);
                }

                if (usedConnectionIds.Count > 0)
                {
                    var usedConnections = await _db.AdPlatformConnections
                        .Where(item => usedConnectionIds.Contains(item.Id))
                        .ToArrayAsync(cancellationToken);
                    foreach (var connection in usedConnections)
                    {
                        connection.LastSyncedAt = now;
                        connection.UpdatedAt = now;
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (guard.UsageLogId.HasValue)
            {
                await _costControlService.FailAsync(guard.UsageLogId.Value, $"Metrics sync failed: {ex.Message}", cancellationToken);
            }

            _logger.LogError(ex, "Ad metrics sync failed for campaign {CampaignId}.", campaignId);
            throw;
        }

        if (guard.UsageLogId.HasValue)
        {
            await _costControlService.CompleteAsync(
                guard.UsageLogId.Value,
                _costEstimator.EstimateQaCost(Math.Max(1, publishedRows.Length)),
                $"Metrics sync completed for {publishedRows.Length} variant(s).",
                cancellationToken);
        }

        var summary = await GetCampaignMetricsSummaryAsync(campaignId, cancellationToken);
        _logger.LogInformation(
            "Ad metrics sync completed for campaign {CampaignId}. Synced variants: {VariantCount}.",
            campaignId,
            publishedRows.Length);
        return new SyncCampaignMetricsResult(campaignId, publishedRows.Length, summary);
    }

    private static ExternalAdMetrics MergeMetrics(ExternalAdMetrics? existing, ExternalAdMetrics current)
    {
        if (existing is null)
        {
            return current;
        }

        return new ExternalAdMetrics(
            existing.Impressions + current.Impressions,
            existing.Clicks + current.Clicks,
            existing.Conversions + current.Conversions,
            existing.CostZar + current.CostZar);
    }

    private static string NormalizeProviderKey(string? value)
    {
        return AdPlatformProviderNormalizer.Normalize(value, fallback: string.Empty);
    }

    public async Task<int> SyncAllPublishedCampaignsAsync(CancellationToken cancellationToken)
    {
        var campaignIds = await (
            from variant in _db.AiAdVariants.AsNoTracking()
            join campaign in _db.Campaigns.AsNoTracking() on variant.CampaignId equals campaign.Id
            join entitlement in _db.PackageBandAiEntitlements.AsNoTracking() on campaign.PackageBandId equals entitlement.PackageBandId
            where variant.Status == "published"
                  && variant.PlatformAdId != null
                  && entitlement.AllowAdMetricsSync
            select variant.CampaignId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        foreach (var campaignId in campaignIds)
        {
            await SyncCampaignMetricsAsync(campaignId, cancellationToken);
        }

        return campaignIds.Length;
    }

    public async Task<OptimizeCampaignResult> OptimizeCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var entitlement = await GetAdEntitlementAsync(campaignId, cancellationToken);
        if (!entitlement.AllowAdAutoOptimize)
        {
            throw new InvalidOperationException("Your package does not include automatic ad optimization.");
        }

        var publishedVariants = await _db.AiAdVariants
            .Where(item => item.CampaignId == campaignId && (item.Status == "published" || item.Status == "promoted"))
            .ToArrayAsync(cancellationToken);

        if (publishedVariants.Length == 0)
        {
            return new OptimizeCampaignResult(
                campaignId,
                null,
                "No published variants are available for optimization.",
                DateTimeOffset.UtcNow);
        }

        var metricRows = await _db.AiAdMetrics
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .ToArrayAsync(cancellationToken);

        var winner = metricRows
            .GroupBy(item => item.AdVariantId)
            .Select(group =>
            {
                var clicks = group.Sum(item => item.Clicks);
                var conversions = group.Sum(item => item.Conversions);
                var score = clicks > 0 ? (decimal)conversions / clicks : 0m;
                return new { VariantId = group.Key, Score = score };
            })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();

        if (winner is null || winner.Score <= 0m)
        {
            return new OptimizeCampaignResult(
                campaignId,
                null,
                "Not enough conversion data to choose a winner yet.",
                DateTimeOffset.UtcNow);
        }

        var now = DateTime.UtcNow;
        foreach (var variant in publishedVariants)
        {
            variant.Status = variant.Id == winner.VariantId ? "promoted" : "published";
            variant.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Optimization promoted variant {VariantId} for campaign {CampaignId}.",
            winner.VariantId,
            campaignId);

        return new OptimizeCampaignResult(
            campaignId,
            winner.VariantId,
            "Best-performing variant promoted based on conversion rate.",
            new DateTimeOffset(now, TimeSpan.Zero));
    }

    private static AdVariantSummary MapVariant(AiAdVariant row)
    {
        return new AdVariantSummary(
            row.Id,
            row.CampaignId,
            row.CampaignCreativeId,
            row.Platform,
            row.Channel,
            row.Language,
            row.TemplateId,
            row.VoicePackId,
            row.VoicePackName,
            row.Script,
            row.AudioAssetUrl,
            row.PlatformAdId,
            row.Status,
            new DateTimeOffset(row.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(row.UpdatedAt, TimeSpan.Zero),
            row.PublishedAt.HasValue ? new DateTimeOffset(row.PublishedAt.Value, TimeSpan.Zero) : null);
    }

    private static string NormalizePlatform(string? value)
    {
        var normalized = NormalizeText(value, "Meta");
        return normalized.Equals("Google", StringComparison.OrdinalIgnoreCase) || normalized.Equals("GoogleAds", StringComparison.OrdinalIgnoreCase)
            ? "GoogleAds"
            : "Meta";
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<PackageAdEntitlement> GetAdEntitlementAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var packageBandId = await _db.Campaigns
            .AsNoTracking()
            .Where(item => item.Id == campaignId)
            .Select(item => (Guid?)item.PackageBandId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!packageBandId.HasValue)
        {
            throw new InvalidOperationException("Campaign not found.");
        }

        var row = await _db.PackageBandAiEntitlements
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PackageBandId == packageBandId.Value, cancellationToken);

        if (row is null)
        {
            throw new InvalidOperationException("Package AI entitlements are not configured for this campaign.");
        }

        var allowedPlatforms = ParseJsonArray(row.AllowedAdPlatformsJson)
            .Select(NormalizePlatform)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedPlatforms.Count == 0)
        {
            allowedPlatforms.Add("Meta");
        }

        var allowedVoicePackTiers = ParseJsonArray(row.AllowedVoicePackTiersJson)
            .Select(item => item.Trim().ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedVoicePackTiers.Count == 0)
        {
            allowedVoicePackTiers.Add("standard");
        }

        return new PackageAdEntitlement(
            Math.Max(1, row.MaxAdVariants),
            allowedPlatforms,
            row.AllowAdMetricsSync,
            row.AllowAdAutoOptimize,
            allowedVoicePackTiers);
    }

    private async Task<CampaignAdPlatformLink?> GetActiveConnectionForCampaignProviderAsync(
        Guid campaignId,
        string platform,
        CancellationToken cancellationToken)
    {
        var providerKey = NormalizeProviderKey(platform);
        var rows = await _db.CampaignAdPlatformLinks
            .Where(item =>
                item.CampaignId == campaignId
                && item.Status == "active"
                && item.AdPlatformConnection.Status == "active")
            .Include(item => item.AdPlatformConnection)
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.UpdatedAt)
            .ToArrayAsync(cancellationToken);
        return rows.FirstOrDefault(item => NormalizeProviderKey(item.AdPlatformConnection.Provider) == providerKey);
    }

    private static IReadOnlyList<string> ParseJsonArray(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(source) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static void EnsurePlatformAllowed(PackageAdEntitlement entitlement, string platform)
    {
        if (!entitlement.AllowedPlatforms.Contains(platform))
        {
            throw new InvalidOperationException(
                $"Platform '{platform}' is not included in your package.");
        }
    }

    private static void EnsureVoiceTierAllowed(PackageAdEntitlement entitlement, string voicePackTier)
    {
        if (!entitlement.AllowedVoicePackTiers.Contains(voicePackTier.Trim().ToLowerInvariant()))
        {
            throw new InvalidOperationException(
                $"Voice pack tier '{voicePackTier}' is not included in your package.");
        }
    }

    private async Task<AiCostGuardDecision> GuardAdOpsCostAsync(
        Guid campaignId,
        Guid? creativeId,
        string operation,
        string provider,
        decimal estimatedCostZar,
        CancellationToken cancellationToken)
    {
        var guard = await _costControlService.GuardAsync(
            new AiCostGuardRequest(
                CampaignId: campaignId,
                Operation: operation,
                Provider: provider,
                EstimatedCostZar: Math.Max(0m, estimatedCostZar),
                CreativeId: creativeId,
                Details: $"{operation} guard"),
            cancellationToken);

        if (guard.Allowed)
        {
            return guard;
        }

        // Hard auto-stop guard: pause active ad variants once cost cap breaches.
        var activeRows = await _db.AiAdVariants
            .Where(item =>
                item.CampaignId == campaignId
                && (item.Status == "published" || item.Status == "promoted" || item.Status == "draft"))
            .ToArrayAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var item in activeRows)
        {
            item.Status = "cost_stopped";
            item.UpdatedAt = now;
        }

        if (activeRows.Length > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogWarning(
            "AI ad-ops auto-stop triggered for campaign {CampaignId}. Operation {Operation} blocked at committed {CommittedCost} / max {MaxAllowed}.",
            campaignId,
            operation,
            guard.CurrentCommittedCostZar,
            guard.MaxAllowedCostZar);
        throw new InvalidOperationException("AI ad operations paused: campaign AI cost cap reached.");
    }

    private sealed record PackageAdEntitlement(
        int MaxAdVariants,
        HashSet<string> AllowedPlatforms,
        bool AllowAdMetricsSync,
        bool AllowAdAutoOptimize,
        HashSet<string> AllowedVoicePackTiers);
}

public sealed class AdPlatformPublisherFactory : IAdPlatformPublisherFactory
{
    private readonly Dictionary<string, IAdPlatformPublisher> _publishers;

    public AdPlatformPublisherFactory(IEnumerable<IAdPlatformPublisher> publishers)
    {
        _publishers = publishers.ToDictionary(
            item => item.Platform.Trim().ToLowerInvariant(),
            item => item,
            StringComparer.OrdinalIgnoreCase);
    }

    public IAdPlatformPublisher GetRequired(string platform)
    {
        var key = AdPlatformProviderNormalizer.Normalize(platform);
        if (_publishers.TryGetValue(key, out var publisher))
        {
            return publisher;
        }

        throw new InvalidOperationException($"No ad platform publisher is registered for '{platform}'.");
    }
}

public sealed class MetaAdPlatformPublisher : IAdPlatformPublisher
{
    private readonly AdPlatformOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MetaAdPlatformPublisher> _logger;

    public MetaAdPlatformPublisher(
        IOptions<AdPlatformOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<MetaAdPlatformPublisher> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Platform => "Meta";

    public async Task<string> PublishAsync(AdVariantSummary variant, CancellationToken cancellationToken, string? accessToken = null)
    {
        if (!_options.DryRunMode && !_options.Meta.Enabled)
        {
            throw new InvalidOperationException("Meta publisher is disabled. Enable AdPlatforms:Meta or use dry-run mode.");
        }

        if (_options.DryRunMode)
        {
            return AdPlatformPublisherHelpers.BuildDeterministicPlatformAdId("meta", variant.Id);
        }

        AdPlatformPublisherHelpers.EnsureProviderConfig(_options.Meta, "Meta", hasAccessTokenOverride: !string.IsNullOrWhiteSpace(accessToken));
        var endpoint = AdPlatformPublisherHelpers.BuildEndpoint(_options.Meta, _options.Meta.PublishPath, variant.PlatformAdId, variant.Id.ToString("D"));
        var body = JsonSerializer.Serialize(new
        {
            accountId = _options.Meta.AccountId,
            campaignId = variant.CampaignId,
            variantId = variant.Id,
            channel = variant.Channel,
            language = variant.Language,
            script = variant.Script,
            audioAssetUrl = variant.AudioAssetUrl
        });

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrWhiteSpace(accessToken) ? _options.Meta.ApiKey : accessToken.Trim());

        var response = await SendAsync("Meta publish", request, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var platformAdId = AdPlatformPublisherHelpers.TryReadString(document.RootElement, "id")
            ?? AdPlatformPublisherHelpers.TryReadString(document.RootElement, "ad_id")
            ?? AdPlatformPublisherHelpers.TryReadString(document.RootElement, "platformAdId");

        if (string.IsNullOrWhiteSpace(platformAdId))
        {
            throw new InvalidOperationException("Meta publish response did not include an ad id.");
        }

        return platformAdId.Trim();
    }

    public async Task<ExternalAdMetrics> GetMetricsAsync(string platformAdId, CancellationToken cancellationToken, string? accessToken = null)
    {
        if (_options.DryRunMode)
        {
            return AdPlatformPublisherHelpers.BuildDeterministicMetrics(platformAdId, 1200, 38, 6, 84m);
        }

        AdPlatformPublisherHelpers.EnsureProviderConfig(_options.Meta, "Meta", hasAccessTokenOverride: !string.IsNullOrWhiteSpace(accessToken));
        var endpoint = AdPlatformPublisherHelpers.BuildEndpoint(_options.Meta, _options.Meta.MetricsPath, platformAdId, platformAdId);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrWhiteSpace(accessToken) ? _options.Meta.ApiKey : accessToken.Trim());
        request.Headers.Accept.ParseAdd("application/json");

        var response = await SendAsync("Meta metrics", request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);

        var metricsRoot = AdPlatformPublisherHelpers.ResolveMetricsRoot(document.RootElement);
        var impressions = AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "impressions") ?? 0;
        var clicks = AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "clicks") ?? 0;
        var conversions = AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "conversions")
            ?? AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "actions")
            ?? 0;
        var cost = AdPlatformPublisherHelpers.TryReadDecimal(metricsRoot, "spend")
            ?? AdPlatformPublisherHelpers.TryReadDecimal(metricsRoot, "cost")
            ?? 0m;

        return new ExternalAdMetrics(impressions, clicks, conversions, cost);
    }

    private async Task<HttpResponseMessage> SendAsync(string operation, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(MetaAdPlatformPublisher));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.Meta.TimeoutSeconds, 5, 120));
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Meta operation {Operation} failed with status {Status}: {Response}", operation, (int)response.StatusCode, content);
            throw new InvalidOperationException($"Meta {operation} failed with status {(int)response.StatusCode}.");
        }

        return response;
    }
}

public sealed class GoogleAdsPlatformPublisher : IAdPlatformPublisher
{
    private readonly AdPlatformOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleAdsPlatformPublisher> _logger;

    public GoogleAdsPlatformPublisher(
        IOptions<AdPlatformOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleAdsPlatformPublisher> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Platform => "GoogleAds";

    public async Task<string> PublishAsync(AdVariantSummary variant, CancellationToken cancellationToken, string? accessToken = null)
    {
        if (!_options.DryRunMode && !_options.GoogleAds.Enabled)
        {
            throw new InvalidOperationException("Google Ads publisher is disabled. Enable AdPlatforms:GoogleAds or use dry-run mode.");
        }

        if (_options.DryRunMode)
        {
            return AdPlatformPublisherHelpers.BuildDeterministicPlatformAdId("gads", variant.Id);
        }

        AdPlatformPublisherHelpers.EnsureProviderConfig(_options.GoogleAds, "GoogleAds", hasAccessTokenOverride: !string.IsNullOrWhiteSpace(accessToken));
        var endpoint = AdPlatformPublisherHelpers.BuildEndpoint(_options.GoogleAds, _options.GoogleAds.PublishPath, variant.PlatformAdId, variant.Id.ToString("D"));
        var body = JsonSerializer.Serialize(new
        {
            customerId = _options.GoogleAds.AccountId,
            campaignId = variant.CampaignId,
            variantId = variant.Id,
            channel = variant.Channel,
            language = variant.Language,
            script = variant.Script,
            audioAssetUrl = variant.AudioAssetUrl
        });

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrWhiteSpace(accessToken) ? _options.GoogleAds.ApiKey : accessToken.Trim());

        var response = await SendAsync("Google Ads publish", request, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var platformAdId = AdPlatformPublisherHelpers.TryReadString(document.RootElement, "id")
            ?? AdPlatformPublisherHelpers.TryReadString(document.RootElement, "adId")
            ?? AdPlatformPublisherHelpers.TryReadString(document.RootElement, "resourceName")
            ?? AdPlatformPublisherHelpers.TryReadString(document.RootElement, "platformAdId");

        if (string.IsNullOrWhiteSpace(platformAdId))
        {
            throw new InvalidOperationException("Google Ads publish response did not include an ad id.");
        }

        return platformAdId.Trim();
    }

    public async Task<ExternalAdMetrics> GetMetricsAsync(string platformAdId, CancellationToken cancellationToken, string? accessToken = null)
    {
        if (_options.DryRunMode)
        {
            return AdPlatformPublisherHelpers.BuildDeterministicMetrics(platformAdId, 950, 26, 5, 63m);
        }

        AdPlatformPublisherHelpers.EnsureProviderConfig(_options.GoogleAds, "GoogleAds", hasAccessTokenOverride: !string.IsNullOrWhiteSpace(accessToken));
        var endpoint = AdPlatformPublisherHelpers.BuildEndpoint(_options.GoogleAds, _options.GoogleAds.MetricsPath, platformAdId, platformAdId);
        var query = "SELECT metrics.impressions, metrics.clicks, metrics.conversions, metrics.cost_micros FROM ad_group_ad WHERE ad_group_ad.ad.id = @adId";
        var body = JsonSerializer.Serialize(new
        {
            customerId = _options.GoogleAds.AccountId,
            adId = platformAdId,
            query
        });

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrWhiteSpace(accessToken) ? _options.GoogleAds.ApiKey : accessToken.Trim());

        var response = await SendAsync("Google Ads metrics", request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        var metricsRoot = AdPlatformPublisherHelpers.ResolveMetricsRoot(document.RootElement);

        var impressions = AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "impressions") ?? 0;
        var clicks = AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "clicks") ?? 0;
        var conversions = AdPlatformPublisherHelpers.TryReadInt(metricsRoot, "conversions") ?? 0;
        var costMicros = AdPlatformPublisherHelpers.TryReadDecimal(metricsRoot, "cost_micros");
        var cost = costMicros.HasValue ? decimal.Round(costMicros.Value / 1_000_000m, 2) : (AdPlatformPublisherHelpers.TryReadDecimal(metricsRoot, "cost") ?? 0m);

        return new ExternalAdMetrics(impressions, clicks, conversions, cost);
    }

    private async Task<HttpResponseMessage> SendAsync(string operation, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(GoogleAdsPlatformPublisher));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.GoogleAds.TimeoutSeconds, 5, 120));
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Ads operation {Operation} failed with status {Status}: {Response}", operation, (int)response.StatusCode, content);
            throw new InvalidOperationException($"Google Ads {operation} failed with status {(int)response.StatusCode}.");
        }

        return response;
    }
}

internal static class AdPlatformPublisherHelpers
{
    public static string BuildDeterministicPlatformAdId(string prefix, Guid variantId)
    {
        return $"{prefix}-{variantId:N}".Substring(0, Math.Min(17, $"{prefix}-{variantId:N}".Length));
    }

    public static ExternalAdMetrics BuildDeterministicMetrics(string seed, int baseImpressions, int baseClicks, int baseConversions, decimal baseCost)
    {
        var hash = Math.Abs(seed.GetHashCode());
        var impressions = baseImpressions + (hash % 900);
        var clicks = baseClicks + (hash % 45);
        var conversions = baseConversions + (hash % 9);
        var cost = baseCost + decimal.Round((hash % 500) / 10m, 2);
        return new ExternalAdMetrics(impressions, clicks, conversions, cost);
    }

    public static void EnsureProviderConfig(AdPlatformProviderOptions provider, string label, bool hasAccessTokenOverride = false)
    {
        if (!provider.Enabled)
        {
            throw new InvalidOperationException($"{label} provider is disabled.");
        }

        if (!hasAccessTokenOverride && string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new InvalidOperationException($"{label} api key is required.");
        }

        if (string.IsNullOrWhiteSpace(provider.AccountId))
        {
            throw new InvalidOperationException($"{label} account id is required.");
        }

        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            throw new InvalidOperationException($"{label} base URL is required.");
        }
    }

    public static string BuildEndpoint(AdPlatformProviderOptions provider, string pathTemplate, string? adId, string fallbackAdId)
    {
        var safePath = (pathTemplate ?? string.Empty)
            .Replace("{accountId}", Uri.EscapeDataString(provider.AccountId), StringComparison.OrdinalIgnoreCase)
            .Replace("{adId}", Uri.EscapeDataString(string.IsNullOrWhiteSpace(adId) ? fallbackAdId : adId), StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(safePath))
        {
            throw new InvalidOperationException("Provider endpoint path is required.");
        }

        return provider.BaseUrl.TrimEnd('/') + "/" + safePath.TrimStart('/');
    }

    public static JsonElement ResolveMetricsRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array
            && data.GetArrayLength() > 0)
        {
            return data[0];
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("results", out var results)
            && results.ValueKind == JsonValueKind.Array
            && results.GetArrayLength() > 0)
        {
            var first = results[0];
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("metrics", out var nestedMetrics))
            {
                return nestedMetrics;
            }

            return first;
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("metrics", out var metrics)
            && metrics.ValueKind == JsonValueKind.Object)
        {
            return metrics;
        }

        return root;
    }

    public static string? TryReadString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    public static int? TryReadInt(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static decimal? TryReadDecimal(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

public sealed class AdMetricsSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AdPlatformOptions _options;
    private readonly ILogger<AdMetricsSyncWorker> _logger;

    public AdMetricsSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AdPlatformOptions> options,
        ILogger<AdMetricsSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(5, _options.MetricsSyncIntervalMinutes);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IAdVariantService>();
                var syncedCampaigns = await service.SyncAllPublishedCampaignsAsync(stoppingToken);
                _logger.LogInformation("Ad metrics sync cycle completed. Campaigns synced: {Count}", syncedCampaigns);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ad metrics sync cycle failed.");
            }
        }
    }
}

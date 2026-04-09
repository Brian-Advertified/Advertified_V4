using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadPaidMediaEvidenceSyncService : ILeadPaidMediaEvidenceSyncService
{
    private static readonly SemaphoreSlim SyncGate = new(1, 1);

    private readonly AppDbContext _db;
    private readonly ISignalCollectorService _signalCollectorService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly LeadIntelligenceAutomationOptions _options;
    private readonly AdPlatformOptions _adPlatformOptions;
    private readonly ILogger<LeadPaidMediaEvidenceSyncService> _logger;

    public LeadPaidMediaEvidenceSyncService(
        AppDbContext db,
        ISignalCollectorService signalCollectorService,
        IChangeAuditService changeAuditService,
        IOptions<AdPlatformOptions> adPlatformOptions,
        IOptions<LeadIntelligenceAutomationOptions> options,
        ILogger<LeadPaidMediaEvidenceSyncService> logger)
    {
        _db = db;
        _signalCollectorService = signalCollectorService;
        _changeAuditService = changeAuditService;
        _adPlatformOptions = adPlatformOptions.Value;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LeadPaidMediaSyncRunResult> SyncBatchAsync(CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var enabledProviders = GetEnabledProviderKeys();

        if (enabledProviders.Count == 0)
        {
            var skipped = BuildSkippedResult(startedAtUtc, "No paid media providers are enabled.", enabledProviders);
            await WriteAuditAsync(skipped, cancellationToken);
            return skipped;
        }

        if (!await SyncGate.WaitAsync(0, cancellationToken))
        {
            var skipped = BuildSkippedResult(startedAtUtc, "A paid media sync run is already in progress.", enabledProviders);
            await WriteAuditAsync(skipped, cancellationToken);
            return skipped;
        }

        try
        {
            var leadIds = await _db.Leads
                .AsNoTracking()
                .Where(lead => lead.Website != null && lead.Website != string.Empty)
                .Select(lead => new
                {
                    lead.Id,
                    LastSignalAt = _db.Signals
                        .Where(signal => signal.LeadId == lead.Id)
                        .Select(signal => (DateTime?)signal.CreatedAt)
                        .Max()
                })
                .OrderBy(item => item.LastSignalAt ?? DateTime.MinValue)
                .ThenBy(item => item.Id)
                .Take(Math.Max(1, _options.BatchSize))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

            var processed = 0;
            var failed = 0;
            var evidenceRows = 0;
            var providerEvidenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var leadId in leadIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var signal = await _signalCollectorService.CollectAsync(leadId, cancellationToken);
                    var sourceCounts = await _db.LeadSignalEvidences
                        .AsNoTracking()
                        .Where(item => item.SignalId == signal.Id)
                        .GroupBy(item => item.Source)
                        .Select(group => new { Source = group.Key, Count = group.Count() })
                        .ToListAsync(cancellationToken);

                    evidenceRows += sourceCounts.Sum(item => item.Count);
                    foreach (var sourceCount in sourceCounts)
                    {
                        if (providerEvidenceCounts.TryGetValue(sourceCount.Source, out var currentCount))
                        {
                            providerEvidenceCounts[sourceCount.Source] = currentCount + sourceCount.Count;
                        }
                        else
                        {
                            providerEvidenceCounts[sourceCount.Source] = sourceCount.Count;
                        }
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Paid media evidence sync failed for lead {LeadId}.", leadId);
                    failed++;
                }
            }

            var finishedAtUtc = DateTime.UtcNow;
            var result = new LeadPaidMediaSyncRunResult
            {
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                TotalLeadCount = leadIds.Count,
                ProcessedLeadCount = processed,
                FailedLeadCount = failed,
                EvidenceRowCount = evidenceRows,
                EnabledProviders = enabledProviders,
                ProviderEvidenceCounts = providerEvidenceCounts,
            };

            await WriteAuditAsync(result, cancellationToken);
            return result;
        }
        finally
        {
            SyncGate.Release();
        }
    }

    private List<string> GetEnabledProviderKeys()
    {
        var providers = new List<string>();

        if (_adPlatformOptions.Meta.Enabled)
        {
            providers.Add("meta");
        }

        if (_adPlatformOptions.GoogleAds.Enabled)
        {
            providers.Add("google_ads");
        }

        if (_adPlatformOptions.LinkedIn.Enabled)
        {
            providers.Add("linkedin");
        }

        if (_adPlatformOptions.TikTok.Enabled)
        {
            providers.Add("tiktok");
        }

        return providers;
    }

    private static LeadPaidMediaSyncRunResult BuildSkippedResult(
        DateTime startedAtUtc,
        string reason,
        IReadOnlyList<string> enabledProviders)
    {
        return new LeadPaidMediaSyncRunResult
        {
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = DateTime.UtcNow,
            Skipped = true,
            SkipReason = reason,
            EnabledProviders = enabledProviders,
        };
    }

    private async Task WriteAuditAsync(LeadPaidMediaSyncRunResult result, CancellationToken cancellationToken)
    {
        await _changeAuditService.WriteAsync(
            actorUserId: null,
            scope: "system",
            action: "lead_paid_media_sync_run",
            entityType: "lead_sync",
            entityId: result.FinishedAtUtc.ToString("O"),
            entityLabel: "lead-paid-media-sync",
            summary: result.Skipped
                ? $"Lead paid media sync run skipped: {result.SkipReason}"
                : $"Lead paid media sync run processed {result.ProcessedLeadCount}/{result.TotalLeadCount} leads with {result.FailedLeadCount} failures.",
            metadata: new
            {
                result.StartedAtUtc,
                result.FinishedAtUtc,
                result.Skipped,
                result.SkipReason,
                result.EnabledProviders,
                result.TotalLeadCount,
                result.ProcessedLeadCount,
                result.FailedLeadCount,
                result.EvidenceRowCount,
                result.ProviderEvidenceCounts
            },
            cancellationToken);
    }
}

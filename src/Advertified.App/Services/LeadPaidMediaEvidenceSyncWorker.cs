using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadPaidMediaEvidenceSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeadPaidMediaEvidenceSyncWorker> _logger;

    public LeadPaidMediaEvidenceSyncWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LeadPaidMediaEvidenceSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialSnapshot = await GetSnapshotAsync(stoppingToken);
        if (!initialSnapshot.EnablePaidMediaEvidenceSync)
        {
            _logger.LogInformation("Lead paid media evidence sync worker is disabled. Polling settings for future enablement.");
        }

        if (initialSnapshot.EnablePaidMediaEvidenceSync && initialSnapshot.RunOnStartup)
        {
            await RunIterationAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await GetSnapshotAsync(stoppingToken);
                var interval = TimeSpan.FromMinutes(Math.Max(15, snapshot.PaidMediaSyncIntervalMinutes));
                using var timer = new PeriodicTimer(interval);
                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
                }

                if (!snapshot.EnablePaidMediaEvidenceSync)
                {
                    continue;
                }

                await RunIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lead paid media evidence sync worker iteration failed.");
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILeadPaidMediaEvidenceSyncService>();
        var result = await service.SyncBatchAsync(cancellationToken);
        if (result.Skipped)
        {
            _logger.LogInformation(
                "Lead paid media evidence sync skipped. Reason: {Reason}. Enabled providers: {Providers}",
                result.SkipReason ?? "n/a",
                result.EnabledProviders.Count > 0 ? string.Join(", ", result.EnabledProviders) : "none");
            return;
        }

        _logger.LogInformation(
            "Lead paid media evidence sync processed {ProcessedCount}/{TotalCount} leads with {FailedCount} failures and {EvidenceCount} evidence rows.",
            result.ProcessedLeadCount,
            result.TotalLeadCount,
            result.FailedLeadCount,
            result.EvidenceRowCount);
    }

    private async Task<LeadIntelligenceAutomationOptions> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<LeadIntelligenceAutomationSnapshotProvider>();
        cancellationToken.ThrowIfCancellationRequested();
        return provider.GetCurrent();
    }
}

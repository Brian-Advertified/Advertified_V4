using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadIntelligenceRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeadIntelligenceRefreshWorker> _logger;

    public LeadIntelligenceRefreshWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LeadIntelligenceRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialSnapshot = await GetSnapshotAsync(stoppingToken);
        if (!initialSnapshot.Enabled)
        {
            _logger.LogInformation("Lead intelligence refresh worker is disabled. Polling settings for future enablement.");
        }

        if (initialSnapshot.Enabled && initialSnapshot.RunOnStartup)
        {
            await RunIterationAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await GetSnapshotAsync(stoppingToken);
                var interval = TimeSpan.FromMinutes(Math.Max(5, snapshot.RefreshIntervalMinutes));
                using var timer = new PeriodicTimer(interval);
                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
                }

                if (!snapshot.Enabled)
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
                _logger.LogError(ex, "Lead intelligence refresh worker iteration failed.");
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ILeadIntelligenceOrchestrator>();
        var processedCount = await orchestrator.RunAllAsync(cancellationToken);
        _logger.LogInformation("Lead intelligence refresh worker processed {ProcessedCount} leads.", processedCount);
    }

    private async Task<LeadIntelligenceAutomationOptions> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<LeadIntelligenceAutomationSnapshotProvider>();
        cancellationToken.ThrowIfCancellationRequested();
        return provider.GetCurrent();
    }
}

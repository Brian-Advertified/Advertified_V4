using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadPaidMediaEvidenceSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LeadIntelligenceAutomationOptions _options;
    private readonly ILogger<LeadPaidMediaEvidenceSyncWorker> _logger;

    public LeadPaidMediaEvidenceSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<LeadIntelligenceAutomationOptions> options,
        ILogger<LeadPaidMediaEvidenceSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnablePaidMediaEvidenceSync)
        {
            _logger.LogInformation("Lead paid media evidence sync worker is disabled.");
            return;
        }

        if (_options.RunOnStartup)
        {
            await RunIterationAsync(stoppingToken);
        }

        var interval = TimeSpan.FromMinutes(Math.Max(15, _options.PaidMediaSyncIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
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
        var processed = await service.SyncBatchAsync(cancellationToken);
        _logger.LogInformation("Lead paid media evidence sync processed {ProcessedCount} leads.", processed);
    }
}

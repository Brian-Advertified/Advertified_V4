using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadIntelligenceRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LeadIntelligenceAutomationOptions _options;
    private readonly ILogger<LeadIntelligenceRefreshWorker> _logger;

    public LeadIntelligenceRefreshWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<LeadIntelligenceAutomationOptions> options,
        ILogger<LeadIntelligenceRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Lead intelligence refresh worker is disabled.");
            return;
        }

        if (_options.RunOnStartup)
        {
            await RunIterationAsync(stoppingToken);
        }

        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.RefreshIntervalMinutes));
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
}

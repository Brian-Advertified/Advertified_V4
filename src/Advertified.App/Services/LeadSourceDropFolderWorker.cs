using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadSourceDropFolderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LeadSourceDropFolderOptions _options;
    private readonly ILogger<LeadSourceDropFolderWorker> _logger;

    public LeadSourceDropFolderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<LeadSourceDropFolderOptions> options,
        ILogger<LeadSourceDropFolderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Lead source drop-folder worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.PollIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIterationAsync(stoppingToken);

                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lead source drop-folder worker iteration failed.");
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<ILeadSourceDropFolderProcessor>();
        var result = await processor.ProcessAsync(cancellationToken);

        if (result.ProcessedFileCount > 0 || result.FailedFileCount > 0)
        {
            _logger.LogInformation(
                "Lead source drop-folder processed {ProcessedFileCount} files, failed {FailedFileCount}, imported {ImportedLeadCount}, analyzed {AnalyzedLeadCount}.",
                result.ProcessedFileCount,
                result.FailedFileCount,
                result.ImportedLeadCount,
                result.AnalyzedLeadCount);
        }
    }
}

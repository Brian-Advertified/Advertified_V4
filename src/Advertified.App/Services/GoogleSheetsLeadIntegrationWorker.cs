using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class GoogleSheetsLeadIntegrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GoogleSheetsLeadOpsOptions _options;
    private readonly ILogger<GoogleSheetsLeadIntegrationWorker> _logger;

    public GoogleSheetsLeadIntegrationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<GoogleSheetsLeadOpsOptions> options,
        ILogger<GoogleSheetsLeadIntegrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Google Sheets lead integration worker is disabled.");
            return;
        }

        var tickIntervalMinutes = Math.Max(1, Math.Min(
            _options.ImportEnabled ? Math.Max(1, _options.ImportPollIntervalMinutes) : int.MaxValue,
            _options.ExportEnabled ? Math.Max(1, _options.ExportPollIntervalMinutes) : int.MaxValue));
        if (tickIntervalMinutes == int.MaxValue)
        {
            _logger.LogInformation("Google Sheets lead integration worker has no enabled tasks.");
            return;
        }

        var importInterval = TimeSpan.FromMinutes(Math.Max(1, _options.ImportPollIntervalMinutes));
        var exportInterval = TimeSpan.FromMinutes(Math.Max(1, _options.ExportPollIntervalMinutes));
        var lastImportAt = DateTimeOffset.MinValue;
        var lastExportAt = DateTimeOffset.MinValue;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(tickIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IGoogleSheetsLeadIntegrationService>();
                var now = DateTimeOffset.UtcNow;

                if (_options.ImportEnabled && now - lastImportAt >= importInterval)
                {
                    await service.ImportConfiguredSourcesAsync(stoppingToken);
                    lastImportAt = now;
                }

                if (_options.ExportEnabled && now - lastExportAt >= exportInterval)
                {
                    await service.ExportLeadOpsSnapshotAsync(stoppingToken);
                    lastExportAt = now;
                }

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
                _logger.LogError(ex, "Google Sheets lead integration worker iteration failed.");
            }
        }
    }
}

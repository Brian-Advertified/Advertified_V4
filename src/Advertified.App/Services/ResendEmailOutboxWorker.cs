using Advertified.App.Configuration;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class ResendEmailOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailOutboxWorker> _logger;

    public ResendEmailOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(2, _options.WorkerPollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<ResendEmailOutboxDispatcher>();
                var processed = await dispatcher.DispatchPendingAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email outbox worker iteration failed.");
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}

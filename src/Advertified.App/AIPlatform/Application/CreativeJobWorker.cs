using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Advertified.App.Configuration;

namespace Advertified.App.AIPlatform.Application;

public sealed class CreativeJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICreativeJobQueue _queue;
    private readonly ILogger<CreativeJobWorker> _logger;
    private readonly AiPlatformOptions _options;

    public CreativeJobWorker(
        IServiceScopeFactory scopeFactory,
        ICreativeJobQueue queue,
        ILogger<CreativeJobWorker> logger,
        IOptions<AiPlatformOptions> options)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in _queue.DequeueAllAsync(stoppingToken))
        {
            var request = envelope.Request;
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ICreativeCampaignOrchestrator>();
            var maxAttempts = Math.Max(1, _options.MaxWorkerRetries);

            if (_options.UseInMemoryFallback)
            {
                var attempts = 0;
                var completed = false;

                while (!completed && !stoppingToken.IsCancellationRequested && attempts < maxAttempts)
                {
                    try
                    {
                        await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                            request.JobId,
                            request.Command.CampaignId,
                            "running",
                            null,
                            RetryAttemptCount: Math.Max(1, attempts + 1),
                            LastFailure: null,
                            DateTimeOffset.UtcNow), stoppingToken);

                        await orchestrator.GenerateAsync(request.Command, stoppingToken);

                        await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                            request.JobId,
                            request.Command.CampaignId,
                            "completed",
                            null,
                            RetryAttemptCount: Math.Max(1, attempts + 1),
                            LastFailure: null,
                            DateTimeOffset.UtcNow), stoppingToken);
                        await envelope.CompleteAsync(stoppingToken);
                        completed = true;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        _logger.LogError(ex, "Creative job {JobId} failed on attempt {Attempt} for campaign {CampaignId}", request.JobId, attempts, request.Command.CampaignId);
                        if (attempts >= maxAttempts)
                        {
                            await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                                request.JobId,
                                request.Command.CampaignId,
                                "failed",
                                ex.Message,
                                RetryAttemptCount: attempts,
                                LastFailure: ex.Message,
                                DateTimeOffset.UtcNow), stoppingToken);
                            await envelope.DeadLetterAsync("processing_failed", ex.Message, stoppingToken);
                            await _queue.MoveToDeadLetterAsync(request.JobId, request.Command.CampaignId, ex.Message, stoppingToken);
                        }
                        else
                        {
                            await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                                request.JobId,
                                request.Command.CampaignId,
                                "retrying",
                                ex.Message,
                                RetryAttemptCount: attempts,
                                LastFailure: ex.Message,
                                DateTimeOffset.UtcNow), stoppingToken);
                            var delaySeconds = Math.Max(1, _options.BaseRetryDelaySeconds) * Math.Pow(2, attempts - 1);
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                        }
                    }
                }

                continue;
            }

            try
            {
                await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                    request.JobId,
                    request.Command.CampaignId,
                    "running",
                    null,
                    RetryAttemptCount: Math.Max(1, envelope.DeliveryCount),
                    LastFailure: null,
                    DateTimeOffset.UtcNow), stoppingToken);

                await orchestrator.GenerateAsync(request.Command, stoppingToken);

                await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                    request.JobId,
                    request.Command.CampaignId,
                    "completed",
                    null,
                    RetryAttemptCount: Math.Max(1, envelope.DeliveryCount),
                    LastFailure: null,
                    DateTimeOffset.UtcNow), stoppingToken);
                await envelope.CompleteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Creative job {JobId} failed on delivery {DeliveryCount} for campaign {CampaignId}", request.JobId, envelope.DeliveryCount, request.Command.CampaignId);

                var isFinalDelivery = envelope.DeliveryCount >= maxAttempts;
                if (isFinalDelivery)
                {
                    await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                        request.JobId,
                        request.Command.CampaignId,
                        "failed",
                        ex.Message,
                        RetryAttemptCount: Math.Max(1, envelope.DeliveryCount),
                        LastFailure: ex.Message,
                        DateTimeOffset.UtcNow), stoppingToken);
                    await envelope.DeadLetterAsync("processing_failed", ex.Message, stoppingToken);
                    await _queue.MoveToDeadLetterAsync(request.JobId, request.Command.CampaignId, ex.Message, stoppingToken);
                }
                else
                {
                    await _queue.SetStatusAsync(new QueueCreativeJobStatus(
                        request.JobId,
                        request.Command.CampaignId,
                        "retrying",
                        ex.Message,
                        RetryAttemptCount: Math.Max(1, envelope.DeliveryCount),
                        LastFailure: ex.Message,
                        DateTimeOffset.UtcNow), stoppingToken);
                    await envelope.AbandonAsync(stoppingToken);
                }
            }
        }
    }
}

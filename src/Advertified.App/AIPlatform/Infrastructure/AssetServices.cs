using System.Text.Json;
using System.Threading.Channels;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class InMemoryAssetJobQueue : IAssetJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async IAsyncEnumerable<AssetJobEnvelope> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var jobId in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return new AssetJobEnvelope(
                jobId,
                DeliveryCount: 1,
                CompleteAsync: _ => Task.CompletedTask,
                AbandonAsync: _ => Task.CompletedTask,
                DeadLetterAsync: (_, _, _) => Task.CompletedTask);
        }
    }
}

public sealed class DbAssetJobRepository : IAssetJobRepository, IAssetJobService
{
    private readonly AppDbContext _db;

    public DbAssetJobRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AssetJobStatusResult> CreateAsync(
        Guid campaignId,
        Guid creativeId,
        string assetKind,
        string provider,
        string requestJson,
        CancellationToken cancellationToken)
    {
        var row = new AiAssetJob
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            CreativeId = creativeId,
            AssetKind = assetKind,
            Provider = provider,
            Status = "queued",
            RequestJson = requestJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.AiAssetJobs.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<AssetJobStatusResult?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var row = await _db.AiAssetJobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public Task<AssetJobStatusResult?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return GetAsync(jobId, cancellationToken);
    }

    public async Task MarkRunningAsync(Guid jobId, int? attemptCount, CancellationToken cancellationToken)
    {
        var row = await _db.AiAssetJobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Asset job '{jobId}' not found.");
        row.Status = "running";
        if (attemptCount.HasValue && attemptCount.Value > 0)
        {
            row.RetryAttemptCount = Math.Max(row.RetryAttemptCount, attemptCount.Value);
        }
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRetryingAsync(Guid jobId, string error, int attemptCount, CancellationToken cancellationToken)
    {
        var row = await _db.AiAssetJobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Asset job '{jobId}' not found.");
        row.Status = "retrying";
        row.Error = error;
        row.RetryAttemptCount = Math.Max(row.RetryAttemptCount, attemptCount);
        row.LastFailure = error;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(Guid jobId, string assetUrl, string assetType, string resultJson, int? attemptCount, CancellationToken cancellationToken)
    {
        var row = await _db.AiAssetJobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Asset job '{jobId}' not found.");
        row.Status = "completed";
        row.AssetUrl = assetUrl;
        row.AssetType = assetType;
        row.ResultJson = resultJson;
        row.Error = null;
        if (attemptCount.HasValue && attemptCount.Value > 0)
        {
            row.RetryAttemptCount = Math.Max(row.RetryAttemptCount, attemptCount.Value);
        }
        row.UpdatedAt = DateTime.UtcNow;
        row.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, int attemptCount, CancellationToken cancellationToken)
    {
        var row = await _db.AiAssetJobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Asset job '{jobId}' not found.");
        row.Status = "failed";
        row.Error = error;
        row.RetryAttemptCount = Math.Max(row.RetryAttemptCount, attemptCount);
        row.LastFailure = error;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssetJobPayload?> GetPayloadAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var row = await _db.AiAssetJobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        return row is null
            ? null
            : new AssetJobPayload(row.Id, row.CampaignId, row.CreativeId, row.AssetKind, row.Provider, row.RequestJson);
    }

    private static AssetJobStatusResult Map(AiAssetJob row)
    {
        return new AssetJobStatusResult(
            row.Id,
            row.CampaignId,
            row.CreativeId,
            row.AssetKind,
            row.Status,
            row.AssetUrl,
            row.AssetType,
            row.Error,
            row.RetryAttemptCount,
            row.LastFailure,
            new DateTimeOffset(row.UpdatedAt, TimeSpan.Zero),
            row.CompletedAt.HasValue ? new DateTimeOffset(row.CompletedAt.Value, TimeSpan.Zero) : null);
    }
}

public sealed class VoiceAssetGenerationService : IVoiceAssetGenerationService
{
    private readonly IAssetJobRepository _assetJobRepository;
    private readonly IAssetJobQueue _assetJobQueue;

    public VoiceAssetGenerationService(IAssetJobRepository assetJobRepository, IAssetJobQueue assetJobQueue)
    {
        _assetJobRepository = assetJobRepository;
        _assetJobQueue = assetJobQueue;
    }

    public async Task<AssetJobQueuedResult> QueueAsync(VoiceAssetRequest request, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(request);
        var created = await _assetJobRepository.CreateAsync(
            request.CampaignId,
            request.CreativeId,
            "voice",
            "ElevenLabs",
            requestJson,
            cancellationToken);
        await _assetJobQueue.EnqueueAsync(created.JobId, cancellationToken);
        return new AssetJobQueuedResult(created.JobId, request.CampaignId, request.CreativeId, "voice", "queued", DateTimeOffset.UtcNow);
    }
}

public sealed class ImageAssetGenerationService : IImageAssetGenerationService
{
    private readonly IAssetJobRepository _assetJobRepository;
    private readonly IAssetJobQueue _assetJobQueue;

    public ImageAssetGenerationService(IAssetJobRepository assetJobRepository, IAssetJobQueue assetJobQueue)
    {
        _assetJobRepository = assetJobRepository;
        _assetJobQueue = assetJobQueue;
    }

    public async Task<AssetJobQueuedResult> QueueAsync(ImageAssetRequest request, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(request);
        var created = await _assetJobRepository.CreateAsync(
            request.CampaignId,
            request.CreativeId,
            "image",
            "ImageApi",
            requestJson,
            cancellationToken);
        await _assetJobQueue.EnqueueAsync(created.JobId, cancellationToken);
        return new AssetJobQueuedResult(created.JobId, request.CampaignId, request.CreativeId, "image", "queued", DateTimeOffset.UtcNow);
    }
}

public sealed class VideoAssetGenerationService : IVideoAssetGenerationService
{
    private readonly IAssetJobRepository _assetJobRepository;
    private readonly IAssetJobQueue _assetJobQueue;

    public VideoAssetGenerationService(IAssetJobRepository assetJobRepository, IAssetJobQueue assetJobQueue)
    {
        _assetJobRepository = assetJobRepository;
        _assetJobQueue = assetJobQueue;
    }

    public async Task<AssetJobQueuedResult> QueueAsync(VideoAssetRequest request, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(request);
        var created = await _assetJobRepository.CreateAsync(
            request.CampaignId,
            request.CreativeId,
            "video",
            "Runway",
            requestJson,
            cancellationToken);
        await _assetJobQueue.EnqueueAsync(created.JobId, cancellationToken);
        return new AssetJobQueuedResult(created.JobId, request.CampaignId, request.CreativeId, "video", "queued", DateTimeOffset.UtcNow);
    }
}

public sealed class AssetJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAssetJobQueue _assetJobQueue;
    private readonly ILogger<AssetJobWorker> _logger;
    private readonly AiPlatformOptions _options;

    public AssetJobWorker(
        IServiceScopeFactory scopeFactory,
        IAssetJobQueue assetJobQueue,
        ILogger<AssetJobWorker> logger,
        IOptions<AiPlatformOptions> options)
    {
        _scopeFactory = scopeFactory;
        _assetJobQueue = assetJobQueue;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var envelope in _assetJobQueue.DequeueAllAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessEnvelopeAsync(envelope, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Asset job worker suppressed an unhandled exception for job {JobId}.", envelope.JobId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Asset job worker loop failed unexpectedly. Restarting queue pump.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessEnvelopeAsync(AssetJobEnvelope envelope, CancellationToken stoppingToken)
    {
        var jobId = envelope.JobId;
        var maxAttempts = Math.Max(1, _options.MaxWorkerRetries);
        if (_options.UseInMemoryFallback)
        {
            var attempts = 0;
            var completed = false;

            while (!completed && !stoppingToken.IsCancellationRequested && attempts < maxAttempts)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAssetJobRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IMultiAiProviderOrchestrator>();

                try
                {
                    var attemptCount = Math.Max(1, attempts + 1);
                    await repository.MarkRunningAsync(jobId, attemptCount, stoppingToken);
                    var payload = await repository.GetPayloadAsync(jobId, stoppingToken)
                        ?? throw new InvalidOperationException($"Asset job payload '{jobId}' was not found.");

                    var channel = payload.AssetKind switch
                    {
                        "voice" => AdvertisingChannel.Radio,
                        "video" => AdvertisingChannel.Tv,
                        _ => AdvertisingChannel.Billboard
                    };

                    var operation = payload.AssetKind switch
                    {
                        "voice" => "asset-voice",
                        "video" => "asset-video",
                        _ => "asset-image"
                    };

                    var output = await orchestrator.ExecuteAsync(channel, operation, payload.RequestJson, stoppingToken);
                    using var doc = JsonDocument.Parse(output);
                    var assetUrl = doc.RootElement.TryGetProperty("assetUrl", out var assetUrlElement)
                        ? assetUrlElement.GetString() ?? string.Empty
                        : string.Empty;
                    var assetType = doc.RootElement.TryGetProperty("assetType", out var assetTypeElement)
                        ? assetTypeElement.GetString() ?? payload.AssetKind
                        : payload.AssetKind;

                    await repository.MarkCompletedAsync(jobId, assetUrl, assetType, output, attemptCount, stoppingToken);
                    await envelope.CompleteAsync(stoppingToken);
                    completed = true;
                }
                catch (Exception ex)
                {
                    attempts++;
                    _logger.LogError(
                        ex,
                        "Asset job {JobId} failed on attempt {Attempt} of {MaxAttempts}.",
                        jobId,
                        attempts,
                        maxAttempts);

                    if (attempts >= maxAttempts)
                    {
                        await repository.MarkFailedAsync(jobId, ex.Message, attempts, stoppingToken);
                        await envelope.DeadLetterAsync("processing_failed", ex.Message, stoppingToken);
                        break;
                    }

                    await repository.MarkRetryingAsync(jobId, ex.Message, attempts, stoppingToken);
                    var delaySeconds = Math.Max(1, _options.BaseRetryDelaySeconds) * Math.Pow(2, attempts - 1);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
            }

            return;
        }

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAssetJobRepository>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IMultiAiProviderOrchestrator>();

            try
            {
                var attemptCount = Math.Max(1, envelope.DeliveryCount);
                await repository.MarkRunningAsync(jobId, attemptCount, stoppingToken);
                var payload = await repository.GetPayloadAsync(jobId, stoppingToken)
                    ?? throw new InvalidOperationException($"Asset job payload '{jobId}' was not found.");

                var channel = payload.AssetKind switch
                {
                    "voice" => AdvertisingChannel.Radio,
                    "video" => AdvertisingChannel.Tv,
                    _ => AdvertisingChannel.Billboard
                };

                var operation = payload.AssetKind switch
                {
                    "voice" => "asset-voice",
                    "video" => "asset-video",
                    _ => "asset-image"
                };

                var output = await orchestrator.ExecuteAsync(channel, operation, payload.RequestJson, stoppingToken);
                using var doc = JsonDocument.Parse(output);
                var assetUrl = doc.RootElement.TryGetProperty("assetUrl", out var assetUrlElement)
                    ? assetUrlElement.GetString() ?? string.Empty
                    : string.Empty;
                var assetType = doc.RootElement.TryGetProperty("assetType", out var assetTypeElement)
                    ? assetTypeElement.GetString() ?? payload.AssetKind
                    : payload.AssetKind;

                await repository.MarkCompletedAsync(jobId, assetUrl, assetType, output, attemptCount, stoppingToken);
                await envelope.CompleteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Asset job {JobId} failed on delivery {DeliveryCount}.", jobId, envelope.DeliveryCount);

                var isFinalDelivery = envelope.DeliveryCount >= maxAttempts;
                if (isFinalDelivery)
                {
                    await repository.MarkFailedAsync(jobId, ex.Message, Math.Max(1, envelope.DeliveryCount), stoppingToken);
                    await envelope.DeadLetterAsync("processing_failed", ex.Message, stoppingToken);
                }
                else
                {
                    await repository.MarkRetryingAsync(jobId, ex.Message, Math.Max(1, envelope.DeliveryCount), stoppingToken);
                    await envelope.AbandonAsync(stoppingToken);
                }
            }
        }
    }
}

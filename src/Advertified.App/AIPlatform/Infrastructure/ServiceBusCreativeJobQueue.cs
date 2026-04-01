using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Advertified.App.AIPlatform.Application;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class ServiceBusCreativeJobQueue : ICreativeJobQueue, IAsyncDisposable
{
    private readonly AiPlatformOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly Channel<QueueCreativeJobRequest> _fallbackChannel = Channel.CreateUnbounded<QueueCreativeJobRequest>();
    private readonly ConcurrentDictionary<Guid, QueueCreativeJobStatus> _fallbackStatuses = new();

    public ServiceBusCreativeJobQueue(
        IOptions<AiPlatformOptions> options,
        IServiceScopeFactory scopeFactory)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;

        if (!_options.UseInMemoryFallback && string.IsNullOrWhiteSpace(_options.ServiceBusConnectionString))
        {
            throw new InvalidOperationException("AiPlatform:ServiceBusConnectionString is required when UseInMemoryFallback is false.");
        }

        if (!_options.UseInMemoryFallback && !string.IsNullOrWhiteSpace(_options.ServiceBusConnectionString))
        {
            _serviceBusClient = new ServiceBusClient(_options.ServiceBusConnectionString);
        }
    }

    public async ValueTask EnqueueAsync(QueueCreativeJobRequest request, CancellationToken cancellationToken)
    {
        if (_options.UseInMemoryFallback)
        {
            await _fallbackChannel.Writer.WriteAsync(request, cancellationToken);
            _fallbackStatuses[request.JobId] = new QueueCreativeJobStatus(
                request.JobId,
                request.Command.CampaignId,
                "queued",
                null,
                RetryAttemptCount: 0,
                LastFailure: null,
                DateTimeOffset.UtcNow);
            return;
        }

        ArgumentNullException.ThrowIfNull(_serviceBusClient);
        var sender = _serviceBusClient.CreateSender(_options.QueueName);
        var body = JsonSerializer.Serialize(request);
        var message = new ServiceBusMessage(body)
        {
            MessageId = request.JobId.ToString("D"),
            ContentType = "application/json"
        };

        await sender.SendMessageAsync(message, cancellationToken);
        await SetStatusAsync(new QueueCreativeJobStatus(
            request.JobId,
            request.Command.CampaignId,
            "queued",
            null,
            RetryAttemptCount: 0,
            LastFailure: null,
            DateTimeOffset.UtcNow), cancellationToken);
    }

    public async IAsyncEnumerable<QueueCreativeJobEnvelope> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_options.UseInMemoryFallback)
        {
            await foreach (var request in _fallbackChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return new QueueCreativeJobEnvelope(
                    request,
                    DeliveryCount: 1,
                    CompleteAsync: _ => Task.CompletedTask,
                    AbandonAsync: _ => Task.CompletedTask,
                    DeadLetterAsync: (_, _, _) => Task.CompletedTask);
            }

            yield break;
        }

        ArgumentNullException.ThrowIfNull(_serviceBusClient);
        var receiver = _serviceBusClient.CreateReceiver(_options.QueueName);
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
            if (message is null)
            {
                continue;
            }

            var request = JsonSerializer.Deserialize<QueueCreativeJobRequest>(message.Body.ToString());
            if (request is null)
            {
                await receiver.DeadLetterMessageAsync(message, "invalid_payload", "Unable to deserialize QueueCreativeJobRequest", cancellationToken);
                continue;
            }

            yield return new QueueCreativeJobEnvelope(
                request,
                message.DeliveryCount,
                CompleteAsync: token => receiver.CompleteMessageAsync(message, token),
                AbandonAsync: token => receiver.AbandonMessageAsync(message, cancellationToken: token),
                DeadLetterAsync: (reason, description, token) => receiver.DeadLetterMessageAsync(message, reason, description, token));
        }
    }

    public async Task SetStatusAsync(QueueCreativeJobStatus status, CancellationToken cancellationToken)
    {
        if (_options.UseInMemoryFallback)
        {
            _fallbackStatuses[status.JobId] = status;
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.AiCreativeJobStatuses
            .FirstOrDefaultAsync(item => item.JobId == status.JobId, cancellationToken);

        if (existing is null)
        {
            db.AiCreativeJobStatuses.Add(new AiCreativeJobStatus
            {
                JobId = status.JobId,
                CampaignId = status.CampaignId,
                Status = status.Status,
                Error = status.Error,
                RetryAttemptCount = status.RetryAttemptCount,
                LastFailure = status.LastFailure ?? status.Error,
                UpdatedAt = status.UpdatedAt.UtcDateTime
            });
        }
        else
        {
            existing.Status = status.Status;
            existing.Error = status.Error;
            existing.RetryAttemptCount = Math.Max(existing.RetryAttemptCount, status.RetryAttemptCount);
            if (!string.IsNullOrWhiteSpace(status.LastFailure))
            {
                existing.LastFailure = status.LastFailure;
            }
            else if (!string.IsNullOrWhiteSpace(status.Error) && (status.Status == "retrying" || status.Status == "failed"))
            {
                existing.LastFailure = status.Error;
            }
            existing.UpdatedAt = status.UpdatedAt.UtcDateTime;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<QueueCreativeJobStatus?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (_options.UseInMemoryFallback)
        {
            _fallbackStatuses.TryGetValue(jobId, out var status);
            return status;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.AiCreativeJobStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.JobId == jobId, cancellationToken);

        return entity is null
            ? null
            : new QueueCreativeJobStatus(
                entity.JobId,
                entity.CampaignId,
                entity.Status,
                entity.Error,
                entity.RetryAttemptCount,
                entity.LastFailure,
                new DateTimeOffset(entity.UpdatedAt, TimeSpan.Zero));
    }

    public async Task MoveToDeadLetterAsync(Guid jobId, Guid campaignId, string reason, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AiCreativeJobDeadLetters.Add(new AiCreativeJobDeadLetter
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            CampaignId = campaignId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceBusClient is not null)
        {
            await _serviceBusClient.DisposeAsync();
        }
    }
}

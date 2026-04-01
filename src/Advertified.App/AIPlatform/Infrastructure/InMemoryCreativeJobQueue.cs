using System.Collections.Concurrent;
using System.Threading.Channels;
using Advertified.App.AIPlatform.Application;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class InMemoryCreativeJobQueue : ICreativeJobQueue
{
    private readonly Channel<QueueCreativeJobRequest> _channel = Channel.CreateUnbounded<QueueCreativeJobRequest>();
    private readonly ConcurrentDictionary<Guid, QueueCreativeJobStatus> _statuses = new();
    private readonly ConcurrentDictionary<Guid, string> _deadLetters = new();

    public async ValueTask EnqueueAsync(QueueCreativeJobRequest request, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<QueueCreativeJobEnvelope> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return new QueueCreativeJobEnvelope(
                request,
                DeliveryCount: 1,
                CompleteAsync: _ => Task.CompletedTask,
                AbandonAsync: _ => Task.CompletedTask,
                DeadLetterAsync: (_, _, _) => Task.CompletedTask);
        }
    }

    public Task SetStatusAsync(QueueCreativeJobStatus status, CancellationToken cancellationToken)
    {
        _statuses[status.JobId] = status;
        return Task.CompletedTask;
    }

    public Task<QueueCreativeJobStatus?> GetStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _statuses.TryGetValue(jobId, out var status);
        return Task.FromResult(status);
    }

    public Task MoveToDeadLetterAsync(Guid jobId, Guid campaignId, string reason, CancellationToken cancellationToken)
    {
        _deadLetters[jobId] = reason;
        return Task.CompletedTask;
    }
}

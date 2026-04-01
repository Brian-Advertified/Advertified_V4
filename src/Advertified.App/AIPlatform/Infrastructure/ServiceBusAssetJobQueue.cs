using System.Threading.Channels;
using Advertified.App.AIPlatform.Application;
using Advertified.App.Configuration;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class ServiceBusAssetJobQueue : IAssetJobQueue, IAsyncDisposable
{
    private readonly AiPlatformOptions _options;
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly Channel<Guid> _fallbackChannel = Channel.CreateUnbounded<Guid>();

    public ServiceBusAssetJobQueue(IOptions<AiPlatformOptions> options)
    {
        _options = options.Value;

        if (!_options.UseInMemoryFallback && string.IsNullOrWhiteSpace(_options.ServiceBusConnectionString))
        {
            throw new InvalidOperationException("AiPlatform:ServiceBusConnectionString is required when UseInMemoryFallback is false.");
        }

        if (!_options.UseInMemoryFallback && !string.IsNullOrWhiteSpace(_options.ServiceBusConnectionString))
        {
            _serviceBusClient = new ServiceBusClient(_options.ServiceBusConnectionString);
        }
    }

    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (_options.UseInMemoryFallback)
        {
            await _fallbackChannel.Writer.WriteAsync(jobId, cancellationToken);
            return;
        }

        ArgumentNullException.ThrowIfNull(_serviceBusClient);
        var sender = _serviceBusClient.CreateSender(_options.AssetQueueName);
        var message = new ServiceBusMessage(jobId.ToString("D"))
        {
            MessageId = jobId.ToString("D"),
            ContentType = "text/plain"
        };

        await sender.SendMessageAsync(message, cancellationToken);
    }

    public async IAsyncEnumerable<AssetJobEnvelope> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_options.UseInMemoryFallback)
        {
            await foreach (var jobId in _fallbackChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return new AssetJobEnvelope(
                    jobId,
                    DeliveryCount: 1,
                    CompleteAsync: _ => Task.CompletedTask,
                    AbandonAsync: _ => Task.CompletedTask,
                    DeadLetterAsync: (_, _, _) => Task.CompletedTask);
            }

            yield break;
        }

        ArgumentNullException.ThrowIfNull(_serviceBusClient);
        var receiver = _serviceBusClient.CreateReceiver(_options.AssetQueueName);

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
            if (message is null)
            {
                continue;
            }

            var body = message.Body.ToString();
            if (!Guid.TryParse(body, out var jobId))
            {
                await receiver.DeadLetterMessageAsync(
                    message,
                    "invalid_asset_job_id",
                    "Asset job message body is not a valid Guid.",
                    cancellationToken);
                continue;
            }

            yield return new AssetJobEnvelope(
                jobId,
                message.DeliveryCount,
                CompleteAsync: token => receiver.CompleteMessageAsync(message, token),
                AbandonAsync: token => receiver.AbandonMessageAsync(message, cancellationToken: token),
                DeadLetterAsync: (reason, description, token) => receiver.DeadLetterMessageAsync(message, reason, description, token));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceBusClient is not null)
        {
            await _serviceBusClient.DisposeAsync();
        }
    }
}

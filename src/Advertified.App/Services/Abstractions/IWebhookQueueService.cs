using Advertified.App.Contracts.Payments;

namespace Advertified.App.Services.Abstractions;

public interface IWebhookQueueService
{
    Task<bool> EnqueueVodaPayWebhookAsync(QueuedVodaPayWebhookJob job, CancellationToken cancellationToken);
}

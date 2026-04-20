using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class ResendEmailOutboxDispatcher
{
    private const string ProviderKey = "resend";
    private readonly AppDbContext _db;
    private readonly IEmailDeliveryTrackingService _trackingService;
    private readonly ResendEmailTransport _transport;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailOutboxDispatcher> _logger;

    public ResendEmailOutboxDispatcher(
        AppDbContext db,
        IEmailDeliveryTrackingService trackingService,
        ResendEmailTransport transport,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailOutboxDispatcher> logger)
    {
        _db = db;
        _trackingService = trackingService;
        _transport = transport;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lockExpiry = now.AddMinutes(-5);
        var batchSize = Math.Max(1, _options.WorkerBatchSize);
        var claimedMessages = await _db.EmailDeliveryMessages
            .Where(message =>
                message.ProviderKey == ProviderKey
                && (message.Status == EmailDeliveryStatuses.Pending
                    || (message.Status == EmailDeliveryStatuses.Failed
                        && message.NextAttemptAt != null
                        && message.NextAttemptAt <= now))
                && (message.LockedAt == null || message.LockedAt < lockExpiry))
            .OrderBy(message => message.NextAttemptAt ?? message.CreatedAt)
            .ThenBy(message => message.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (claimedMessages.Count == 0)
        {
            return 0;
        }

        foreach (var message in claimedMessages)
        {
            message.AttemptCount += 1;
            message.LastAttemptAt = now;
            message.LockToken = Guid.NewGuid();
            message.LockedAt = now;
            message.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var processed = 0;
        foreach (var message in claimedMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed += await DispatchOneAsync(message, cancellationToken);
        }

        return processed;
    }

    private async Task<int> DispatchOneAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _transport.SendAsync(message, cancellationToken);
            switch (outcome.Outcome)
            {
                case "accepted":
                    await _trackingService.MarkAcceptedAsync(message.Id, outcome.ProviderMessageId!, outcome.ProviderBroadcastId, cancellationToken);
                    break;
                case "archived":
                    await _trackingService.MarkArchivedAsync(message.Id, outcome.ArchivePath!, cancellationToken);
                    break;
                case "retryable_failure":
                    var retryAt = message.AttemptCount >= Math.Max(1, _options.MaxDeliveryAttempts)
                        ? (DateTime?)null
                        : DateTime.UtcNow.AddSeconds(CalculateRetryDelaySeconds(message.AttemptCount));
                    await _trackingService.MarkFailedAsync(
                        message.Id,
                        outcome.ErrorMessage ?? "Provider send failed.",
                        retryAt,
                        cancellationToken);
                    break;
                default:
                    await _trackingService.MarkFailedAsync(
                        message.Id,
                        outcome.ErrorMessage ?? "Provider send failed.",
                        null,
                        cancellationToken);
                    break;
            }

            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email outbox dispatch failed for message {DispatchId}.", message.Id);
            var retryAt = message.AttemptCount >= Math.Max(1, _options.MaxDeliveryAttempts)
                ? (DateTime?)null
                : DateTime.UtcNow.AddSeconds(CalculateRetryDelaySeconds(message.AttemptCount));
            await _trackingService.MarkFailedAsync(message.Id, ex.Message, retryAt, cancellationToken);
            return 1;
        }
    }

    private int CalculateRetryDelaySeconds(int attemptCount)
    {
        var maxAttempts = Math.Max(1, _options.MaxDeliveryAttempts);
        if (attemptCount >= maxAttempts)
        {
            return 0;
        }

        var baseDelay = Math.Max(1, _options.BaseRetryDelaySeconds);
        var exponent = Math.Max(0, attemptCount - 1);
        var delay = baseDelay * Math.Pow(2, exponent);
        return (int)Math.Min(delay, 3600);
    }
}

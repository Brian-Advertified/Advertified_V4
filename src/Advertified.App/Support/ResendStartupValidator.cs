using Advertified.App.Configuration;
using Microsoft.Extensions.Options;

namespace Advertified.App.Support;

internal static class ResendStartupValidator
{
    public static Task ValidateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ResendOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ResendStartupValidator");

        cancellationToken.ThrowIfCancellationRequested();

        if (!ResendConfigurationInspector.HasValidBaseUrl(options))
        {
            throw new InvalidOperationException("Resend startup validation failed: Resend:BaseUrl must be a valid absolute HTTP(S) URL.");
        }

        if (options.WorkerBatchSize <= 0)
        {
            throw new InvalidOperationException("Resend startup validation failed: Resend:WorkerBatchSize must be greater than zero.");
        }

        if (options.WorkerPollSeconds <= 0)
        {
            throw new InvalidOperationException("Resend startup validation failed: Resend:WorkerPollSeconds must be greater than zero.");
        }

        if (options.MaxDeliveryAttempts <= 0)
        {
            throw new InvalidOperationException("Resend startup validation failed: Resend:MaxDeliveryAttempts must be greater than zero.");
        }

        if (options.BaseRetryDelaySeconds <= 0)
        {
            throw new InvalidOperationException("Resend startup validation failed: Resend:BaseRetryDelaySeconds must be greater than zero.");
        }

        if (!ResendConfigurationInspector.HasAnySender(options))
        {
            throw new InvalidOperationException("Resend startup validation failed: configure Resend:FromEmail or at least one Resend:SenderAddresses entry.");
        }

        if (ResendConfigurationInspector.HasApiKey(options))
        {
            logger.LogInformation("Resend startup validation passed. Live email delivery is configured.");
            return Task.CompletedTask;
        }

        if (!options.AllowLocalArchiveFallback)
        {
            throw new InvalidOperationException("Resend startup validation failed: Resend:ApiKey is missing and Resend:AllowLocalArchiveFallback is false.");
        }

        logger.LogWarning(
            "Resend startup validation passed with local archive fallback enabled. Emails will be archived to {ArchiveDirectory} until Resend:ApiKey is configured.",
            string.IsNullOrWhiteSpace(options.LocalArchiveDirectory) ? "App_Data/email_outbox" : options.LocalArchiveDirectory);

        return Task.CompletedTask;
    }
}

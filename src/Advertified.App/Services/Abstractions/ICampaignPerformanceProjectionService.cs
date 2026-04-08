using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignPerformanceProjectionService
{
    Task UpsertAdPlatformMetricsAsync(
        Guid campaignId,
        string platform,
        ExternalAdMetrics metrics,
        DateTime recordedAtUtc,
        string? supplierLabel,
        decimal? attributedRevenueZar,
        CancellationToken cancellationToken);
}

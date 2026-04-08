using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CampaignPerformanceProjectionService : ICampaignPerformanceProjectionService
{
    private readonly AppDbContext _db;

    public CampaignPerformanceProjectionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAdPlatformMetricsAsync(
        Guid campaignId,
        string platform,
        ExternalAdMetrics metrics,
        DateTime recordedAtUtc,
        string? supplierLabel,
        decimal? attributedRevenueZar,
        CancellationToken cancellationToken)
    {
        var normalizedRecordedAt = NormalizeRecordedAtUtc(recordedAtUtc);
        var normalizedMetrics = NormalizeMetrics(metrics);
        var normalizedRevenue = Math.Max(0m, attributedRevenueZar.GetValueOrDefault());
        var platformLabel = ResolvePlatformLabel(platform, supplierLabel);
        var booking = await _db.CampaignSupplierBookings
            .FirstOrDefaultAsync(
                item => item.CampaignId == campaignId
                    && item.Channel == "digital"
                    && item.SupplierOrStation == platformLabel
                    && item.Notes == CampaignPerformanceConstants.SyncedBookingNotes,
                cancellationToken);

        if (booking is null)
        {
            booking = new CampaignSupplierBooking
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                SupplierOrStation = platformLabel,
                Channel = "digital",
                BookingStatus = HasLiveSignals(normalizedMetrics) ? "live" : "booked",
                CommittedAmount = 0m,
                BookedAt = normalizedRecordedAt,
                Notes = CampaignPerformanceConstants.SyncedBookingNotes,
                CreatedAt = normalizedRecordedAt,
                UpdatedAt = normalizedRecordedAt
            };
            _db.CampaignSupplierBookings.Add(booking);
        }
        else
        {
            booking.BookingStatus = HasLiveSignals(normalizedMetrics) ? "live" : "booked";
            booking.UpdatedAt = normalizedRecordedAt;
        }

        var report = await _db.CampaignDeliveryReports
            .Where(item =>
                item.CampaignId == campaignId
                && item.SupplierBookingId == booking.Id
                && item.ReportType == CampaignPerformanceConstants.SyncedReportType
                && item.ReportedAt.HasValue
                && item.ReportedAt.Value >= normalizedRecordedAt.Date
                && item.ReportedAt.Value < normalizedRecordedAt.Date.AddDays(1))
            .FirstOrDefaultAsync(
                cancellationToken);

        var headline = $"{platformLabel} performance";
        var summary = BuildSummary(normalizedMetrics);
        if (report is null)
        {
            report = new CampaignDeliveryReport
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                SupplierBookingId = booking.Id,
                ReportType = CampaignPerformanceConstants.SyncedReportType,
                Headline = headline,
                Summary = summary,
                ReportedAt = normalizedRecordedAt,
                Impressions = normalizedMetrics.Impressions,
                PlaysOrSpots = normalizedMetrics.Clicks,
                SpendDelivered = normalizedMetrics.CostZar,
                CreatedAt = normalizedRecordedAt
            };
            _db.CampaignDeliveryReports.Add(report);
            await UpsertChannelMetricAsync(
                campaignId,
                channel: "digital",
                provider: platformLabel,
                normalizedRecordedAt,
                normalizedMetrics,
                normalizedRevenue,
                cancellationToken);
            return;
        }

        report.Headline = headline;
        report.Summary = summary;
        report.ReportedAt = normalizedRecordedAt;
        report.Impressions = normalizedMetrics.Impressions;
        report.PlaysOrSpots = normalizedMetrics.Clicks;
        report.SpendDelivered = normalizedMetrics.CostZar;

        await UpsertChannelMetricAsync(
            campaignId,
            channel: "digital",
            provider: platformLabel,
            normalizedRecordedAt,
            normalizedMetrics,
            normalizedRevenue,
            cancellationToken);
    }

    private async Task UpsertChannelMetricAsync(
        Guid campaignId,
        string channel,
        string provider,
        DateTime recordedAtUtc,
        ExternalAdMetrics metrics,
        decimal attributedRevenueZar,
        CancellationToken cancellationToken)
    {
        var metricDate = DateOnly.FromDateTime(recordedAtUtc);
        var normalizedChannel = string.IsNullOrWhiteSpace(channel) ? "digital" : channel.Trim().ToLowerInvariant();
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ad platform" : provider.Trim();
        var cpl = metrics.Conversions > 0
            ? decimal.Round(metrics.CostZar / metrics.Conversions, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        var roas = metrics.CostZar > 0m && attributedRevenueZar > 0m
            ? decimal.Round(attributedRevenueZar / metrics.CostZar, 4, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        var row = await _db.CampaignChannelMetrics
            .FirstOrDefaultAsync(item =>
                    item.CampaignId == campaignId
                    && item.Channel == normalizedChannel
                    && item.Provider == normalizedProvider
                    && item.MetricDate == metricDate,
                cancellationToken);

        if (row is null)
        {
            row = new CampaignChannelMetric
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                Channel = normalizedChannel,
                Provider = normalizedProvider,
                MetricDate = metricDate,
                SpendZar = metrics.CostZar,
                Impressions = metrics.Impressions,
                Clicks = metrics.Clicks,
                Leads = metrics.Conversions,
                AttributedRevenueZar = attributedRevenueZar,
                CplZar = cpl,
                Roas = roas,
                SourceType = "ad_platform_sync",
                CreatedAt = recordedAtUtc,
                UpdatedAt = recordedAtUtc
            };
            _db.CampaignChannelMetrics.Add(row);
            return;
        }

        row.SpendZar = metrics.CostZar;
        row.Impressions = metrics.Impressions;
        row.Clicks = metrics.Clicks;
        row.Leads = metrics.Conversions;
        row.AttributedRevenueZar = attributedRevenueZar;
        row.CplZar = cpl;
        row.Roas = roas;
        row.UpdatedAt = recordedAtUtc;
    }

    private static string ResolvePlatformLabel(string platform, string? supplierLabel)
    {
        if (!string.IsNullOrWhiteSpace(supplierLabel))
        {
            return supplierLabel.Trim();
        }

        var normalized = (platform ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "meta" => "Meta Ads",
            "googleads" => "Google Ads",
            "google" => "Google Ads",
            "linkedin" => "LinkedIn Ads",
            "tiktok" => "TikTok Ads",
            _ => string.IsNullOrWhiteSpace(platform) ? "Ad platform" : platform.Trim()
        };
    }

    private static string BuildSummary(ExternalAdMetrics metrics)
    {
        return $"Clicks {metrics.Clicks:n0} | Conversions {metrics.Conversions:n0} | Spend {metrics.CostZar:n2}";
    }

    private static DateTime NormalizeRecordedAtUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static ExternalAdMetrics NormalizeMetrics(ExternalAdMetrics metrics)
    {
        return new ExternalAdMetrics(
            Math.Max(0, metrics.Impressions),
            Math.Max(0, metrics.Clicks),
            Math.Max(0, metrics.Conversions),
            Math.Max(0m, metrics.CostZar));
    }

    private static bool HasLiveSignals(ExternalAdMetrics metrics)
    {
        return metrics.Impressions > 0 || metrics.Clicks > 0 || metrics.Conversions > 0;
    }
}

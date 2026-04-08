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
        CancellationToken cancellationToken)
    {
        var normalizedRecordedAt = NormalizeRecordedAtUtc(recordedAtUtc);
        var normalizedMetrics = NormalizeMetrics(metrics);
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
            return;
        }

        report.Headline = headline;
        report.Summary = summary;
        report.ReportedAt = normalizedRecordedAt;
        report.Impressions = normalizedMetrics.Impressions;
        report.PlaysOrSpots = normalizedMetrics.Clicks;
        report.SpendDelivered = normalizedMetrics.CostZar;
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

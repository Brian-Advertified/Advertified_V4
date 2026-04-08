using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CampaignPerformanceProjectionService : ICampaignPerformanceProjectionService
{
    private const string SyncedBookingNotes = "System-managed ad platform performance sync.";
    private const string SyncedReportType = "ad_platform_sync";
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
        var platformLabel = ResolvePlatformLabel(platform, supplierLabel);
        var booking = await _db.CampaignSupplierBookings
            .FirstOrDefaultAsync(
                item => item.CampaignId == campaignId
                    && item.Channel == "digital"
                    && item.SupplierOrStation == platformLabel
                    && item.Notes == SyncedBookingNotes,
                cancellationToken);

        if (booking is null)
        {
            booking = new CampaignSupplierBooking
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                SupplierOrStation = platformLabel,
                Channel = "digital",
                BookingStatus = metrics.Impressions > 0 || metrics.Clicks > 0 || metrics.Conversions > 0 ? "live" : "booked",
                CommittedAmount = 0m,
                BookedAt = recordedAtUtc,
                Notes = SyncedBookingNotes,
                CreatedAt = recordedAtUtc,
                UpdatedAt = recordedAtUtc
            };
            _db.CampaignSupplierBookings.Add(booking);
        }
        else
        {
            booking.BookingStatus = metrics.Impressions > 0 || metrics.Clicks > 0 || metrics.Conversions > 0 ? "live" : "booked";
            booking.UpdatedAt = recordedAtUtc;
        }

        var report = await _db.CampaignDeliveryReports
            .Where(item =>
                item.CampaignId == campaignId
                && item.SupplierBookingId == booking.Id
                && item.ReportType == SyncedReportType
                && item.ReportedAt.HasValue
                && item.ReportedAt.Value >= recordedAtUtc.Date
                && item.ReportedAt.Value < recordedAtUtc.Date.AddDays(1))
            .FirstOrDefaultAsync(
                cancellationToken);

        var headline = $"{platformLabel} performance";
        var summary = BuildSummary(metrics);
        if (report is null)
        {
            report = new CampaignDeliveryReport
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                SupplierBookingId = booking.Id,
                ReportType = SyncedReportType,
                Headline = headline,
                Summary = summary,
                ReportedAt = recordedAtUtc,
                Impressions = metrics.Impressions,
                PlaysOrSpots = metrics.Clicks,
                SpendDelivered = metrics.CostZar,
                CreatedAt = recordedAtUtc
            };
            _db.CampaignDeliveryReports.Add(report);
            return;
        }

        report.Headline = headline;
        report.Summary = summary;
        report.ReportedAt = recordedAtUtc;
        report.Impressions = metrics.Impressions;
        report.PlaysOrSpots = metrics.Clicks;
        report.SpendDelivered = metrics.CostZar;
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
}

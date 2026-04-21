using Advertified.App.Campaigns;
using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class AgentCampaignBookingOrchestrationService : IAgentCampaignBookingOrchestrationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly ITemplatedEmailService _emailService;
    private readonly ICampaignStatusTransitionService _campaignStatusTransitionService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ILogger<AgentCampaignBookingOrchestrationService> _logger;

    public AgentCampaignBookingOrchestrationService(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAgentCampaignOwnershipService ownershipService,
        ITemplatedEmailService emailService,
        ICampaignStatusTransitionService campaignStatusTransitionService,
        IChangeAuditService changeAuditService,
        ILogger<AgentCampaignBookingOrchestrationService> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _ownershipService = ownershipService;
        _emailService = emailService;
        _campaignStatusTransitionService = campaignStatusTransitionService;
        _changeAuditService = changeAuditService;
        _logger = logger;
    }

    public async Task<AgentCampaignActionResult> SaveSupplierBookingAsync(Guid id, SaveCampaignSupplierBookingRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.User!)
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(request.SupplierOrStation) || string.IsNullOrWhiteSpace(request.Channel))
        {
            return BadRequest(new { message = "Supplier/station and channel are required." });
        }

        var now = DateTime.UtcNow;
        var booking = new CampaignSupplierBooking
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            CreatedByUserId = currentUser.Id,
            ProofAssetId = request.ProofAssetId,
            SupplierOrStation = request.SupplierOrStation.Trim(),
            Channel = request.Channel.Trim(),
            BookingStatus = string.IsNullOrWhiteSpace(request.BookingStatus) ? "planned" : request.BookingStatus.Trim().ToLowerInvariant(),
            CommittedAmount = request.CommittedAmount,
            BookedAt = request.BookedAt?.UtcDateTime,
            LiveFrom = request.LiveFrom,
            LiveTo = request.LiveTo,
            Notes = NormalizeOptionalText(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CampaignSupplierBookings.Add(booking);
        try
        {
            if (!_campaignStatusTransitionService.TryMoveToBookingInProgress(campaign, now))
            {
                campaign.UpdatedAt = now;
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign is not ready for supplier booking.",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "save_supplier_booking",
            "campaign_supplier_booking",
            booking.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Saved supplier booking for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                booking.SupplierOrStation,
                booking.Channel,
                booking.BookingStatus,
                booking.CommittedAmount,
                booking.LiveFrom,
                booking.LiveTo
            },
            cancellationToken);

        await SendSupplierBookingEmailAsync(campaign, booking, cancellationToken);
        return Accepted(new { CampaignId = campaign.Id, SupplierBookingId = booking.Id, Message = "Supplier booking saved." });
    }

    public async Task<AgentCampaignActionResult> SaveDeliveryReportAsync(Guid id, SaveCampaignDeliveryReportRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.User!)
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ReportType) || string.IsNullOrWhiteSpace(request.Headline))
        {
            return BadRequest(new { message = "Report type and headline are required." });
        }

        var now = DateTime.UtcNow;
        var report = new CampaignDeliveryReport
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            SupplierBookingId = request.SupplierBookingId,
            EvidenceAssetId = request.EvidenceAssetId,
            CreatedByUserId = currentUser.Id,
            ReportType = request.ReportType.Trim().ToLowerInvariant(),
            Headline = request.Headline.Trim(),
            Summary = NormalizeOptionalText(request.Summary),
            ReportedAt = request.ReportedAt?.UtcDateTime ?? now,
            Impressions = request.Impressions,
            PlaysOrSpots = request.PlaysOrSpots,
            SpendDelivered = request.SpendDelivered,
            CreatedAt = now
        };

        _db.CampaignDeliveryReports.Add(report);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "save_delivery_report",
            "campaign_delivery_report",
            report.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Saved delivery report for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                report.ReportType,
                report.Headline,
                report.Impressions,
                report.PlaysOrSpots,
                report.SpendDelivered
            },
            cancellationToken);

        await SendDeliveryReportEmailAsync(campaign, report, cancellationToken);
        return Accepted(new { CampaignId = campaign.Id, DeliveryReportId = report.Id, Message = "Delivery report saved." });
    }

    private async Task WriteChangeAuditAsync(
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _changeAuditService.WriteAsync(currentUserId, "agent", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private async Task<UserAccount> GetCurrentOperationsUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (currentUser.Role is not UserRole.Agent and not UserRole.Admin and not UserRole.CreativeDirector)
        {
            throw new ForbiddenException("Agent, creative director, or admin access is required.");
        }

        return currentUser;
    }

    private static string ResolveCampaignLabel(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand?.Name ?? "Campaign"} campaign"
            : campaign.CampaignName.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private async Task SendSupplierBookingEmailAsync(Campaign campaign, CampaignSupplierBooking booking, CancellationToken cancellationToken)
    {
        var campaignUser = RequireCampaignUser(campaign);
        try
        {
            await _emailService.SendAsync(
                "campaign-booking-confirmed",
                campaignUser.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaignUser.FullName,
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["SupplierOrStation"] = booking.SupplierOrStation,
                    ["Channel"] = booking.Channel,
                    ["BookingStatus"] = booking.BookingStatus
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send supplier booking email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendDeliveryReportEmailAsync(Campaign campaign, CampaignDeliveryReport report, CancellationToken cancellationToken)
    {
        var campaignUser = RequireCampaignUser(campaign);
        try
        {
            await _emailService.SendAsync(
                "campaign-report-available",
                campaignUser.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaignUser.FullName,
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["ReportHeadline"] = report.Headline,
                    ["ReportType"] = report.ReportType,
                    ["Impressions"] = report.Impressions?.ToString(),
                    ["PlaysOrSpots"] = report.PlaysOrSpots?.ToString(),
                    ["SpendDelivered"] = report.SpendDelivered?.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-ZA"))
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send delivery report email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private static UserAccount RequireCampaignUser(Campaign campaign)
    {
        return campaign.User
            ?? throw new InvalidOperationException("Campaign is missing its client account.");
    }

    private static AgentCampaignActionResult Accepted(object payload) => new(StatusCodes.Status202Accepted, payload);

    private static AgentCampaignActionResult BadRequest(object payload) => new(StatusCodes.Status400BadRequest, payload);
}

using System.Globalization;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Admin;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Mappings;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/campaign-operations")]
[Authorize(Roles = "Admin")]
public sealed class AdminCampaignOperationsController : ControllerBase
{
    private const int PerformanceAttentionThresholdPercent = 60;
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AdminCampaignOperationsController> _logger;

    public AdminCampaignOperationsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AdminCampaignOperationsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _changeAuditService = changeAuditService;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<AdminCampaignOperationsResponse>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sortBy = "delivery_risk",
        [FromQuery] bool attentionOnly = false,
        CancellationToken cancellationToken = default)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(campaign => new AdminCampaignOperationsListRow
            {
                CampaignId = campaign.Id,
                PackageOrderId = campaign.PackageOrderId,
                CampaignName = campaign.CampaignName,
                CampaignStatus = campaign.Status,
                ClientName = campaign.User != null ? campaign.User.FullName : string.Empty,
                ClientEmail = campaign.User != null ? campaign.User.Email : string.Empty,
                PackageBandName = campaign.PackageBand.Name,
                SelectedBudget = campaign.PackageOrder.SelectedBudget,
                ChargedTotal = campaign.PackageOrder.Amount,
                PaymentStatus = campaign.PackageOrder.PaymentStatus,
                RefundStatus = campaign.PackageOrder.RefundStatus,
                RefundedAmount = campaign.PackageOrder.RefundedAmount,
                GatewayFeeRetainedAmount = campaign.PackageOrder.GatewayFeeRetainedAmount,
                RefundReason = campaign.PackageOrder.RefundReason,
                RefundProcessedAt = campaign.PackageOrder.RefundProcessedAt,
                PausedAt = campaign.PausedAt,
                PauseReason = campaign.PauseReason,
                TotalPausedDays = campaign.TotalPausedDays,
                BriefStartDate = campaign.CampaignBrief != null ? campaign.CampaignBrief.StartDate : null,
                BriefEndDate = campaign.CampaignBrief != null ? campaign.CampaignBrief.EndDate : null,
                BriefDurationWeeks = campaign.CampaignBrief != null ? campaign.CampaignBrief.DurationWeeks : null,
                BookedStartDate = campaign.CampaignSupplierBookings.Where(booking => booking.LiveFrom.HasValue).Min(booking => booking.LiveFrom),
                BookedEndDate = campaign.CampaignSupplierBookings.Where(booking => booking.LiveTo.HasValue).Max(booking => booking.LiveTo),
                PausedDaysFromWindows = campaign.CampaignPauseWindows.Sum(window => window.PausedDayCount),
                PerformanceBookedSpend = campaign.CampaignSupplierBookings.Sum(booking => (decimal?)booking.CommittedAmount) ?? 0m,
                PerformanceDeliveredSpend = campaign.CampaignDeliveryReports.Sum(report => (decimal?)report.SpendDelivered) ?? 0m,
                PerformanceImpressions = campaign.CampaignDeliveryReports.Sum(report => (long?)report.Impressions) ?? 0L,
                PerformancePlaysOrSpots = campaign.CampaignDeliveryReports.Sum(report => (int?)report.PlaysOrSpots) ?? 0,
                PerformanceSyncedClicks = campaign.CampaignDeliveryReports
                    .Where(report => report.ReportType == CampaignPerformanceConstants.SyncedReportType)
                    .Sum(report => (int?)report.PlaysOrSpots) ?? 0,
                PerformanceLatestReportDate = campaign.CampaignDeliveryReports
                    .Where(report => report.ReportedAt.HasValue)
                    .Max(report => (DateTime?)report.ReportedAt)
            })
            .ToArrayAsync(cancellationToken);

        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 10, 100);
        var normalizedSort = NormalizeQueueSort(sortBy);
        var mappedItems = campaigns
            .Select(campaign => MapOperationsItem(campaign, today))
            .ToArray();
        var totalPausedCount = mappedItems.Count(item => item.IsPaused);
        var totalRefundAttentionCount = mappedItems.Count(item =>
            item.CanProcessRefund && string.Equals(item.RefundPolicyStage, "post_delivery_or_live", StringComparison.OrdinalIgnoreCase));
        var totalScheduledCount = mappedItems.Count(item => item.DaysLeft.HasValue);
        var totalPerformanceAttentionCount = mappedItems.Count(NeedsPerformanceAttention);
        var filteredItems = attentionOnly
            ? mappedItems.Where(NeedsPerformanceAttention).ToArray()
            : mappedItems;
        var sortedItems = SortQueueItems(filteredItems, normalizedSort).ToArray();
        var totalCount = sortedItems.Length;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var effectivePage = Math.Min(normalizedPage, totalPages);
        var pagedItems = sortedItems
            .Skip((effectivePage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();

        return Ok(new AdminCampaignOperationsResponse
        {
            Items = pagedItems,
            Page = effectivePage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = effectivePage > 1,
            HasNextPage = effectivePage < totalPages,
            SortBy = normalizedSort,
            AttentionOnly = attentionOnly,
            PerformanceAttentionThresholdPercent = PerformanceAttentionThresholdPercent,
            TotalPausedCount = totalPausedCount,
            TotalRefundAttentionCount = totalRefundAttentionCount,
            TotalScheduledCount = totalScheduledCount,
            TotalPerformanceAttentionCount = totalPerformanceAttentionCount
        });
    }

    [HttpPost("{campaignId:guid}/pause")]
    public async Task<ActionResult<AdminCampaignOperationsItemResponse>> Pause(Guid campaignId, [FromBody] AdminPauseCampaignRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var campaign = await LoadCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }
        if (campaign.PausedAt.HasValue)
        {
            return BadRequest(new { message = "Campaign is already paused." });
        }

        if (string.Equals(campaign.PackageOrder.RefundStatus, "refunded", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Refunded campaigns cannot be paused." });
        }

        var now = DateTime.UtcNow;
        campaign.PausedAt = now;
        campaign.PauseReason = NormalizeOptionalText(request.Reason);
        campaign.UpdatedAt = now;
        _db.CampaignPauseWindows.Add(new CampaignPauseWindow
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            CreatedByUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken),
            StartedAt = now,
            PauseReason = campaign.PauseReason,
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "pause_campaign",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignName(campaign),
            $"Paused {ResolveCampaignName(campaign)}.",
            new { CampaignId = campaign.Id, campaign.PauseReason, campaign.PausedAt },
            cancellationToken);

        await SendCampaignPausedEmailAsync(campaign, cancellationToken);

        return Ok(MapOperationsItem(campaign, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [HttpPost("{campaignId:guid}/unpause")]
    public async Task<ActionResult<AdminCampaignOperationsItemResponse>> Unpause(Guid campaignId, [FromBody] AdminUnpauseCampaignRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var campaign = await LoadCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }
        if (!campaign.PausedAt.HasValue)
        {
            return BadRequest(new { message = "Campaign is not paused." });
        }

        var now = DateTime.UtcNow;
        var pausedDate = DateOnly.FromDateTime(campaign.PausedAt.Value);
        var today = DateOnly.FromDateTime(now);
        var pausedDays = Math.Max(0, today.DayNumber - pausedDate.DayNumber);
        var resumeReason = NormalizeOptionalText(request.Reason);

        campaign.TotalPausedDays += pausedDays;
        campaign.PausedAt = null;
        campaign.PauseReason = resumeReason ?? campaign.PauseReason;
        campaign.UpdatedAt = now;

        var openPauseWindow = await _db.CampaignPauseWindows
            .Where(x => x.CampaignId == campaign.Id && x.EndedAt == null)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (openPauseWindow is not null)
        {
            openPauseWindow.EndedAt = now;
            openPauseWindow.ResumeReason = resumeReason;
            openPauseWindow.PausedDayCount = pausedDays;
            openPauseWindow.ResumedByUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "unpause_campaign",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignName(campaign),
            $"Resumed {ResolveCampaignName(campaign)} after pause.",
            new { CampaignId = campaign.Id, PausedDaysAdded = pausedDays, campaign.TotalPausedDays, campaign.PauseReason },
            cancellationToken);

        await SendCampaignResumedEmailAsync(campaign, pausedDays, cancellationToken);

        return Ok(MapOperationsItem(campaign, today));
    }

    [HttpPost("{campaignId:guid}/refund")]
    public async Task<ActionResult<AdminCampaignOperationsItemResponse>> Refund(Guid campaignId, [FromBody] AdminProcessRefundRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var campaign = await LoadCampaignAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }
        var order = campaign.PackageOrder;
        if (!string.Equals(order.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only paid orders can be refunded." });
        }

        var proposedGatewayFee = Math.Max(order.GatewayFeeRetainedAmount, request.GatewayFeeRetainedAmount ?? 0m);
        order.GatewayFeeRetainedAmount = proposedGatewayFee;

        var snapshot = CampaignOperationsPolicy.BuildRefundSnapshot(campaign);
        if (snapshot.MaxManualRefundAmount <= 0m)
        {
            return BadRequest(new { message = "There is no refundable balance left on this order." });
        }

        decimal refundAmount;
        if (request.Amount.HasValue)
        {
            refundAmount = request.Amount.Value;
        }
        else if (snapshot.Stage == "post_delivery_or_live")
        {
            return BadRequest(new { message = "Enter the unused or uncommitted value to refund for this campaign stage." });
        }
        else
        {
            refundAmount = snapshot.SuggestedRefundAmount;
        }

        if (refundAmount <= 0m)
        {
            return BadRequest(new { message = "Refund amount must be greater than zero." });
        }

        if (refundAmount > snapshot.MaxManualRefundAmount)
        {
            return BadRequest(new { message = "Refund amount exceeds the remaining collected amount." });
        }

        if (order.RefundedAmount + refundAmount + order.GatewayFeeRetainedAmount > order.Amount)
        {
            return BadRequest(new { message = "Refund amount and retained gateway fee exceed the collected total." });
        }

        var now = DateTime.UtcNow;
        order.RefundedAmount += refundAmount;
        order.RefundReason = NormalizeOptionalText(request.Reason);
        order.RefundProcessedAt = now;
        order.UpdatedAt = now;
        order.RefundStatus = ResolveRefundStatus(order);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "process_refund",
            "package_order",
            order.Id.ToString(),
            ResolveCampaignName(campaign),
            $"Processed refund for {ResolveCampaignName(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                PackageOrderId = order.Id,
                RefundAmount = refundAmount,
                order.RefundedAmount,
                order.GatewayFeeRetainedAmount,
                order.RefundStatus,
                order.RefundReason,
                snapshot.Stage
            },
            cancellationToken);

        await SendRefundProcessedEmailAsync(campaign, refundAmount, snapshot, cancellationToken);

        return Ok(MapOperationsItem(campaign, DateOnly.FromDateTime(now)));
    }

    [HttpGet("{campaignId:guid}/performance")]
    public async Task<ActionResult<CampaignPerformanceSnapshotResponse>> GetPerformance(Guid campaignId, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.CampaignChannelMetrics)
            .Include(x => x.CampaignSupplierBookings)
            .Include(x => x.CampaignDeliveryReports)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(campaign.ToPerformanceSnapshot());
    }

    private AdminCampaignOperationsItemResponse MapOperationsItem(AdminCampaignOperationsListRow campaign, DateOnly today)
    {
        var refundSnapshot = CampaignOperationsPolicy.BuildRefundSnapshot(
            campaign.CampaignStatus,
            campaign.ChargedTotal,
            campaign.RefundedAmount,
            campaign.GatewayFeeRetainedAmount,
            campaign.SelectedBudget);
        var scheduleSnapshot = CampaignOperationsPolicy.BuildScheduleSnapshot(
            campaign.BriefStartDate,
            campaign.BriefEndDate,
            campaign.BriefDurationWeeks,
            campaign.BookedStartDate,
            campaign.BookedEndDate,
            campaign.PausedDaysFromWindows,
            campaign.TotalPausedDays,
            campaign.PausedAt,
            today);
        var campaignName = ResolveCampaignName(campaign.CampaignName, campaign.PackageBandName);

        return new AdminCampaignOperationsItemResponse
        {
            CampaignId = campaign.CampaignId,
            PackageOrderId = campaign.PackageOrderId,
            CampaignName = campaignName,
            CampaignStatus = campaign.CampaignStatus,
            ClientName = campaign.ClientName,
            ClientEmail = campaign.ClientEmail,
            PackageBandName = campaign.PackageBandName,
            SelectedBudget = campaign.SelectedBudget ?? campaign.ChargedTotal,
            ChargedTotal = campaign.ChargedTotal,
            PaymentStatus = campaign.PaymentStatus,
            RefundStatus = campaign.RefundStatus,
            RefundedAmount = campaign.RefundedAmount,
            RemainingCollectedAmount = refundSnapshot.RemainingCollectedAmount,
            SuggestedRefundAmount = refundSnapshot.SuggestedRefundAmount,
            MaxManualRefundAmount = refundSnapshot.MaxManualRefundAmount,
            GatewayFeeRetainedAmount = campaign.GatewayFeeRetainedAmount,
            RefundPolicyStage = refundSnapshot.Stage,
            RefundPolicyLabel = refundSnapshot.Label,
            RefundPolicySummary = refundSnapshot.Summary,
            RefundReason = campaign.RefundReason,
            RefundProcessedAt = campaign.RefundProcessedAt.HasValue ? new DateTimeOffset(campaign.RefundProcessedAt.Value, TimeSpan.Zero) : null,
            IsPaused = campaign.PausedAt.HasValue,
            PauseReason = campaign.PauseReason,
            PausedAt = campaign.PausedAt.HasValue ? new DateTimeOffset(campaign.PausedAt.Value, TimeSpan.Zero) : null,
            TotalPausedDays = campaign.TotalPausedDays,
            StartDate = scheduleSnapshot.StartDate,
            EndDate = scheduleSnapshot.EndDate,
            EffectiveEndDate = scheduleSnapshot.EffectiveEndDate,
            DaysLeft = scheduleSnapshot.DaysLeft,
            CanPause = !campaign.PausedAt.HasValue && !string.Equals(campaign.RefundStatus, "refunded", StringComparison.OrdinalIgnoreCase),
            CanUnpause = campaign.PausedAt.HasValue,
            CanProcessRefund = string.Equals(campaign.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) && refundSnapshot.MaxManualRefundAmount > 0m,
            PerformanceBookedSpend = campaign.PerformanceBookedSpend,
            PerformanceDeliveredSpend = campaign.PerformanceDeliveredSpend,
            PerformanceDeliveryPercent = ToDeliveryPercent(campaign.PerformanceBookedSpend, campaign.PerformanceDeliveredSpend),
            PerformanceImpressions = campaign.PerformanceImpressions,
            PerformancePlaysOrSpots = campaign.PerformancePlaysOrSpots,
            PerformanceSyncedClicks = campaign.PerformanceSyncedClicks,
            PerformanceLatestReportDate = campaign.PerformanceLatestReportDate.HasValue
                ? DateOnly.FromDateTime(campaign.PerformanceLatestReportDate.Value)
                : null
        };
    }

    private AdminCampaignOperationsItemResponse MapOperationsItem(Campaign campaign, DateOnly today)
    {
        var campaignUser = RequireCampaignUser(campaign);
        var refundSnapshot = CampaignOperationsPolicy.BuildRefundSnapshot(campaign);
        var scheduleSnapshot = CampaignOperationsPolicy.BuildScheduleSnapshot(campaign, today);
        var campaignName = ResolveCampaignName(campaign);

        return new AdminCampaignOperationsItemResponse
        {
            CampaignId = campaign.Id,
            PackageOrderId = campaign.PackageOrderId,
            CampaignName = campaignName,
            CampaignStatus = campaign.Status,
            ClientName = campaignUser.FullName,
            ClientEmail = campaignUser.Email,
            PackageBandName = campaign.PackageBand.Name,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
            ChargedTotal = campaign.PackageOrder.Amount,
            PaymentStatus = campaign.PackageOrder.PaymentStatus,
            RefundStatus = campaign.PackageOrder.RefundStatus,
            RefundedAmount = campaign.PackageOrder.RefundedAmount,
            RemainingCollectedAmount = refundSnapshot.RemainingCollectedAmount,
            SuggestedRefundAmount = refundSnapshot.SuggestedRefundAmount,
            MaxManualRefundAmount = refundSnapshot.MaxManualRefundAmount,
            GatewayFeeRetainedAmount = campaign.PackageOrder.GatewayFeeRetainedAmount,
            RefundPolicyStage = refundSnapshot.Stage,
            RefundPolicyLabel = refundSnapshot.Label,
            RefundPolicySummary = refundSnapshot.Summary,
            RefundReason = campaign.PackageOrder.RefundReason,
            RefundProcessedAt = campaign.PackageOrder.RefundProcessedAt.HasValue ? new DateTimeOffset(campaign.PackageOrder.RefundProcessedAt.Value, TimeSpan.Zero) : null,
            IsPaused = campaign.PausedAt.HasValue,
            PauseReason = campaign.PauseReason,
            PausedAt = campaign.PausedAt.HasValue ? new DateTimeOffset(campaign.PausedAt.Value, TimeSpan.Zero) : null,
            TotalPausedDays = campaign.TotalPausedDays,
            StartDate = scheduleSnapshot.StartDate,
            EndDate = scheduleSnapshot.EndDate,
            EffectiveEndDate = scheduleSnapshot.EffectiveEndDate,
            DaysLeft = scheduleSnapshot.DaysLeft,
            CanPause = !campaign.PausedAt.HasValue && !string.Equals(campaign.PackageOrder.RefundStatus, "refunded", StringComparison.OrdinalIgnoreCase),
            CanUnpause = campaign.PausedAt.HasValue,
            CanProcessRefund = string.Equals(campaign.PackageOrder.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) && refundSnapshot.MaxManualRefundAmount > 0m,
            PerformanceBookedSpend = campaign.CampaignSupplierBookings.Sum(item => item.CommittedAmount),
            PerformanceDeliveredSpend = campaign.CampaignDeliveryReports.Sum(item => item.SpendDelivered ?? 0m),
            PerformanceDeliveryPercent = ToDeliveryPercent(
                campaign.CampaignSupplierBookings.Sum(item => item.CommittedAmount),
                campaign.CampaignDeliveryReports.Sum(item => item.SpendDelivered ?? 0m)),
            PerformanceImpressions = campaign.CampaignDeliveryReports.Sum(item => item.Impressions ?? 0),
            PerformancePlaysOrSpots = campaign.CampaignDeliveryReports.Sum(item => item.PlaysOrSpots ?? 0),
            PerformanceSyncedClicks = campaign.CampaignDeliveryReports
                .Where(item => string.Equals(item.ReportType, CampaignPerformanceConstants.SyncedReportType, StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.PlaysOrSpots ?? 0),
            PerformanceLatestReportDate = campaign.CampaignDeliveryReports
                .Where(item => item.ReportedAt.HasValue)
                .Select(item => DateOnly.FromDateTime(item.ReportedAt!.Value))
                .OrderBy(item => item)
                .LastOrDefault()
        };
    }

    private async Task<Campaign?> LoadCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.Campaigns
            .Include(x => x.User!)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignSupplierBookings)
            .Include(x => x.CampaignDeliveryReports)
            .Include(x => x.CampaignPauseWindows)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
    }

    private async Task<ActionResult?> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }

        var currentUser = await _db.UserAccounts.FindAsync(new object[] { currentUserId }, cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (currentUser.Role != UserRole.Admin)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
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
        await _changeAuditService.WriteAsync(currentUserId, "admin", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private async Task SendRefundProcessedEmailAsync(Campaign campaign, decimal refundAmount, CampaignOperationsPolicy.RefundPolicySnapshot snapshot, CancellationToken cancellationToken)
    {
        var campaignUser = RequireCampaignUser(campaign);
        try
        {
            await _emailService.SendAsync(
                "refund-processed",
                campaignUser.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaignUser.FullName,
                    ["CampaignName"] = ResolveCampaignName(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["RefundAmount"] = CurrencyFormatSupport.FormatZar(refundAmount),
                    ["RefundStatus"] = campaign.PackageOrder.RefundStatus,
                    ["RefundPolicyLabel"] = snapshot.Label,
                    ["RefundReason"] = campaign.PackageOrder.RefundReason ?? "No reason supplied",
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send refund processed email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendCampaignPausedEmailAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        var campaignUser = RequireCampaignUser(campaign);
        try
        {
            await _emailService.SendAsync(
                "campaign-paused",
                campaignUser.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaignUser.FullName,
                    ["CampaignName"] = ResolveCampaignName(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["PauseReason"] = campaign.PauseReason ?? "No reason supplied",
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send paused email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendCampaignResumedEmailAsync(Campaign campaign, int pausedDays, CancellationToken cancellationToken)
    {
        var campaignUser = RequireCampaignUser(campaign);
        try
        {
            var schedule = CampaignOperationsPolicy.BuildScheduleSnapshot(campaign, DateOnly.FromDateTime(DateTime.UtcNow));
            await _emailService.SendAsync(
                "campaign-resumed",
                campaignUser.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaignUser.FullName,
                    ["CampaignName"] = ResolveCampaignName(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["PausedDays"] = pausedDays.ToString(CultureInfo.InvariantCulture),
                    ["DaysLeft"] = schedule.DaysLeft?.ToString(CultureInfo.InvariantCulture) ?? "Not scheduled",
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send resumed email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string ResolveRefundStatus(PackageOrder order)
    {
        var resolvedAmount = order.RefundedAmount + order.GatewayFeeRetainedAmount;
        if (resolvedAmount >= order.Amount)
        {
            return "refunded";
        }

        return order.RefundedAmount > 0m ? "partial" : "none";
    }

    private static string ResolveCampaignName(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand.Name} campaign"
            : campaign.CampaignName.Trim();
    }

    private static string ResolveCampaignName(string? campaignName, string packageBandName)
    {
        return string.IsNullOrWhiteSpace(campaignName)
            ? $"{packageBandName} campaign"
            : campaignName.Trim();
    }

    private static UserAccount RequireCampaignUser(Campaign campaign)
    {
        return campaign.User
            ?? throw new InvalidOperationException("Campaign is missing its client account.");
    }

    private sealed class AdminCampaignOperationsListRow
    {
        public Guid CampaignId { get; init; }
        public Guid PackageOrderId { get; init; }
        public string? CampaignName { get; init; }
        public string CampaignStatus { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string ClientEmail { get; init; } = string.Empty;
        public string PackageBandName { get; init; } = string.Empty;
        public decimal? SelectedBudget { get; init; }
        public decimal ChargedTotal { get; init; }
        public string PaymentStatus { get; init; } = string.Empty;
        public string RefundStatus { get; init; } = string.Empty;
        public decimal RefundedAmount { get; init; }
        public decimal GatewayFeeRetainedAmount { get; init; }
        public string? RefundReason { get; init; }
        public DateTime? RefundProcessedAt { get; init; }
        public DateTime? PausedAt { get; init; }
        public string? PauseReason { get; init; }
        public int TotalPausedDays { get; init; }
        public DateOnly? BriefStartDate { get; init; }
        public DateOnly? BriefEndDate { get; init; }
        public int? BriefDurationWeeks { get; init; }
        public DateOnly? BookedStartDate { get; init; }
        public DateOnly? BookedEndDate { get; init; }
        public int PausedDaysFromWindows { get; init; }
        public decimal PerformanceBookedSpend { get; init; }
        public decimal PerformanceDeliveredSpend { get; init; }
        public long PerformanceImpressions { get; init; }
        public int PerformancePlaysOrSpots { get; init; }
        public int PerformanceSyncedClicks { get; init; }
        public DateTime? PerformanceLatestReportDate { get; init; }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static int ToDeliveryPercent(decimal bookedSpend, decimal deliveredSpend)
    {
        if (bookedSpend <= 0m || deliveredSpend <= 0m)
        {
            return 0;
        }

        var ratio = deliveredSpend / bookedSpend;
        if (ratio >= 1m)
        {
            return 100;
        }

        return (int)Math.Round(ratio * 100m, MidpointRounding.AwayFromZero);
    }

    private static bool NeedsPerformanceAttention(AdminCampaignOperationsItemResponse item)
    {
        if (item.PerformanceBookedSpend <= 0m)
        {
            return false;
        }

        return item.PerformanceLatestReportDate is null || item.PerformanceDeliveryPercent < PerformanceAttentionThresholdPercent;
    }

    private static IEnumerable<AdminCampaignOperationsItemResponse> SortQueueItems(
        IEnumerable<AdminCampaignOperationsItemResponse> items,
        string sortBy)
    {
        return sortBy switch
        {
            "highest_spend" => items
                .OrderByDescending(item => item.PerformanceBookedSpend)
                .ThenBy(item => item.CampaignName),
            "latest_update" => items
                .OrderByDescending(item => item.PerformanceLatestReportDate)
                .ThenBy(item => item.CampaignName),
            "campaign_name" => items
                .OrderBy(item => item.CampaignName),
            _ => items
                .OrderByDescending(GetDeliveryRiskScore)
                .ThenByDescending(item => item.PerformanceBookedSpend)
                .ThenBy(item => item.CampaignName)
        };
    }

    private static int GetDeliveryRiskScore(AdminCampaignOperationsItemResponse item)
    {
        if (item.PerformanceBookedSpend <= 0m)
        {
            return -1000;
        }

        var score = 100 - item.PerformanceDeliveryPercent;
        if (item.PerformanceLatestReportDate is null)
        {
            score += 40;
        }

        return score;
    }

    private static string NormalizeQueueSort(string? sortBy)
    {
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "highest_spend" => "highest_spend",
            "latest_update" => "latest_update",
            "campaign_name" => "campaign_name",
            _ => "delivery_risk"
        };
    }
}

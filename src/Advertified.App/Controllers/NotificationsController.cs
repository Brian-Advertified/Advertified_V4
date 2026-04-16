using Advertified.App.Contracts.Notifications;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAdminDashboardService _adminDashboardService;

    public NotificationsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAdminDashboardService adminDashboardService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _adminDashboardService = adminDashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<NotificationSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        var items = await GetCurrentNotificationItemsAsync(currentUser, cancellationToken);

        return Ok(new NotificationSummaryResponse
        {
            UnreadCount = items.Count(item => !item.IsRead),
            Items = items
        });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(string id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        var items = await GetCurrentNotificationItemsAsync(currentUser, cancellationToken);
        if (!items.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
        {
            return NotFound(new { message = "Notification could not be found." });
        }

        await MarkNotificationsReadAsync(currentUser.Id, new[] { id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        var items = await GetCurrentNotificationItemsAsync(currentUser, cancellationToken);
        var unreadIds = items
            .Where(item => !item.IsRead)
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (unreadIds.Length > 0)
        {
            await MarkNotificationsReadAsync(currentUser.Id, unreadIds, cancellationToken);
        }

        return NoContent();
    }

    private async Task<UserAccount> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        return await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("Authenticated user account could not be found.");
    }

    private async Task<NotificationSummaryItemResponse[]> GetCurrentNotificationItemsAsync(UserAccount currentUser, CancellationToken cancellationToken)
    {
        var items = currentUser.Role switch
        {
            UserRole.Client => await BuildClientNotificationsAsync(currentUser.Id, cancellationToken),
            UserRole.Agent => await BuildAgentNotificationsAsync(currentUser.Id, false, cancellationToken),
            UserRole.CreativeDirector => await BuildCreativeNotificationsAsync(cancellationToken),
            UserRole.Admin => await BuildAdminNotificationsAsync(cancellationToken),
            _ => Array.Empty<NotificationSummaryItemResponse>()
        };

        return await ApplyReadStateAsync(currentUser.Id, items, cancellationToken);
    }

    private async Task<NotificationSummaryItemResponse[]> ApplyReadStateAsync(
        Guid userId,
        IReadOnlyList<NotificationSummaryItemResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return Array.Empty<NotificationSummaryItemResponse>();
        }

        var itemIds = items
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var readIds = await _db.NotificationReadReceipts
            .AsNoTracking()
            .Where(receipt => receipt.UserId == userId && itemIds.Contains(receipt.NotificationId))
            .Select(receipt => receipt.NotificationId)
            .ToListAsync(cancellationToken);

        var readIdSet = readIds.ToHashSet(StringComparer.Ordinal);
        return items
            .Select(item => new NotificationSummaryItemResponse
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Href = item.Href,
                Tone = item.Tone,
                IsRead = readIdSet.Contains(item.Id)
            })
            .ToArray();
    }

    private async Task MarkNotificationsReadAsync(Guid userId, IReadOnlyCollection<string> notificationIds, CancellationToken cancellationToken)
    {
        if (notificationIds.Count == 0)
        {
            return;
        }

        var distinctIds = notificationIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return;
        }

        var existingIds = await _db.NotificationReadReceipts
            .Where(receipt => receipt.UserId == userId && distinctIds.Contains(receipt.NotificationId))
            .Select(receipt => receipt.NotificationId)
            .ToListAsync(cancellationToken);

        var existingIdSet = existingIds.ToHashSet(StringComparer.Ordinal);
        var newReceipts = distinctIds
            .Where(id => !existingIdSet.Contains(id))
            .Select(id => new NotificationReadReceipt
            {
                UserId = userId,
                NotificationId = id,
                ReadAt = DateTime.UtcNow
            })
            .ToArray();

        if (newReceipts.Length == 0)
        {
            return;
        }

        _db.NotificationReadReceipts.AddRange(newReceipts);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<NotificationSummaryItemResponse[]> BuildClientNotificationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Where(campaign => campaign.UserId == userId)
            .Include(campaign => campaign.PackageBand)
            .Include(campaign => campaign.CampaignRecommendations)
            .OrderByDescending(campaign => campaign.UpdatedAt)
            .Take(6)
            .ToArrayAsync(cancellationToken);

        var orders = await _db.PackageOrders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .Include(order => order.PackageBand)
            .Include(order => order.Invoice)
            .OrderByDescending(order => order.CreatedAt)
            .Take(6)
            .ToArrayAsync(cancellationToken);

        var items = new List<NotificationSummaryItemResponse>();
        foreach (var campaign in campaigns)
        {
            if (campaign is null)
            {
                continue;
            }

            var packageBandName = string.IsNullOrWhiteSpace(campaign.PackageBand?.Name) ? "Package" : campaign.PackageBand.Name.Trim();
            var campaignName = string.IsNullOrWhiteSpace(campaign.CampaignName)
                ? $"{packageBandName} campaign"
                : campaign.CampaignName.Trim();
            var recommendation = (campaign.CampaignRecommendations ?? Array.Empty<CampaignRecommendation>())
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (campaign.Status == CampaignStatuses.Paid)
            {
                items.Add(BuildItem($"campaign-paid-{campaign.Id}", "Campaign workspace ready", $"{campaignName} is paid and ready in your simplified client workspace.", $"/campaigns/{campaign.Id}", "info"));
            }
            else if (campaign.Status == CampaignStatuses.ReviewReady && string.Equals(recommendation?.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(BuildItem($"campaign-review-{campaign.Id}", "Recommendation ready for review", $"{campaignName} is ready for your approval or change request.", $"/campaigns/{campaign.Id}", "success"));
            }
            else if (campaign.Status == CampaignStatuses.PlanningInProgress)
            {
                items.Add(BuildItem($"campaign-planning-{campaign.Id}", "Recommendation in progress", $"{campaignName} is currently being prepared.", $"/campaigns/{campaign.Id}", "info"));
            }
            else if (campaign.Status == CampaignStatuses.Approved)
            {
                items.Add(BuildItem($"campaign-approved-{campaign.Id}", "Recommendation approved", $"{campaignName} is approved and ready for the next step.", $"/campaigns/{campaign.Id}", "success"));
            }
            else if (campaign.Status == CampaignStatuses.CreativeChangesRequested)
            {
                items.Add(BuildItem($"campaign-creative-changes-{campaign.Id}", "Creative changes requested", $"{campaignName} has been sent back for creative revision.", $"/campaigns/{campaign.Id}", "warning"));
            }
            else if (campaign.Status == CampaignStatuses.CreativeSentToClientForApproval)
            {
                items.Add(BuildItem($"campaign-creative-review-{campaign.Id}", "Finished media ready for approval", $"{campaignName} has been sent back for your final approval.", $"/campaigns/{campaign.Id}", "success"));
            }
            else if (campaign.Status == CampaignStatuses.CreativeApproved)
            {
                items.Add(BuildItem($"campaign-creative-approved-{campaign.Id}", "Creative approved", $"{campaignName} finished creative approval and is moving into booking preparation.", $"/campaigns/{campaign.Id}", "success"));
            }
            else if (campaign.Status == CampaignStatuses.BookingInProgress)
            {
                items.Add(BuildItem($"campaign-booking-{campaign.Id}", "Booking in progress", $"{campaignName} is now being booked with suppliers ahead of launch.", $"/campaigns/{campaign.Id}", "info"));
            }
            else if (campaign.Status == CampaignStatuses.Launched)
            {
                items.Add(BuildItem($"campaign-live-{campaign.Id}", "Campaign live", $"{campaignName} is now live.", $"/campaigns/{campaign.Id}", "success"));
            }
        }

        foreach (var order in orders)
        {
            if (order is null)
            {
                continue;
            }

            var packageBandName = string.IsNullOrWhiteSpace(order.PackageBand?.Name) ? "your package" : order.PackageBand.Name.Trim();
            if (string.Equals(order.PaymentStatus, "failed", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(BuildItem($"order-failed-{order.Id}", "Payment was not successful", $"{packageBandName} could not be confirmed. You can try again or contact support.", "/orders", "warning"));
            }
            else if (string.Equals(order.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) && order.Invoice is not null)
            {
                items.Add(BuildItem($"invoice-ready-{order.Id}", "Invoice ready", $"Your paid invoice for {packageBandName} is available.", "/orders", "success"));
            }
        }

        return items.Take(6).ToArray();
    }

    private async Task<NotificationSummaryItemResponse[]> BuildAgentNotificationsAsync(Guid userId, bool creativeOnly, CancellationToken cancellationToken)
    {
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(campaign => campaign.User)
            .Include(campaign => campaign.AssignedAgentUser)
            .Include(campaign => campaign.PackageBand)
            .Include(campaign => campaign.PackageOrder)
            .Include(campaign => campaign.CampaignBrief)
            .Include(campaign => campaign.CampaignRecommendations)
            .Include(campaign => campaign.CampaignConversation!)
                .ThenInclude(conversation => conversation.Messages)
            .OrderByDescending(campaign => campaign.UpdatedAt)
            .Take(12)
            .ToArrayAsync(cancellationToken);

        var items = new List<NotificationSummaryItemResponse>();
        foreach (var campaign in campaigns)
        {
            if (campaign is null)
            {
                continue;
            }

            if (!creativeOnly && campaign.AssignedAgentUserId.HasValue && campaign.AssignedAgentUserId != userId)
            {
                continue;
            }

            var stage = CampaignWorkflowPolicy.ResolveAgentQueueStage(campaign);
            var latestRecommendation = (campaign.CampaignRecommendations ?? Array.Empty<CampaignRecommendation>())
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
            var unreadClientMessages = campaign.CampaignConversation?.Messages.Count(
                message => message.SenderRole == "client" && message.ReadByAgentAt == null) ?? 0;
            var packageBandName = string.IsNullOrWhiteSpace(campaign.PackageBand?.Name) ? "Package" : campaign.PackageBand.Name.Trim();
            var campaignName = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{packageBandName} campaign" : campaign.CampaignName.Trim();

            if (creativeOnly)
            {
                if (campaign.Status == CampaignStatuses.Approved
                    || campaign.Status == CampaignStatuses.CreativeChangesRequested
                    || campaign.Status == CampaignStatuses.CreativeSentToClientForApproval
                    || campaign.Status == CampaignStatuses.BookingInProgress
                    || campaign.Status == CampaignStatuses.CreativeApproved)
                {
                    items.Add(BuildItem($"creative-studio-{campaign.Id}", "Creative studio work ready", $"{campaignName} is ready for production, booking, or creative revision handling.", $"/creative/campaigns/{campaign.Id}/studio", "info"));
                }
                continue;
            }

            if (unreadClientMessages > 0)
            {
                var messageLabel = unreadClientMessages == 1 ? "message" : "messages";
                items.Add(BuildItem(
                    $"agent-client-message-{campaign.Id}",
                    "New client comment",
                    $"{campaignName} has {unreadClientMessages} unread client {messageLabel} for you.",
                    $"/agent/messages?campaignId={campaign.Id}",
                    "info"));
            }

            if (campaign.Status == CampaignStatuses.Approved)
            {
                items.Add(BuildItem(
                    $"agent-approved-{campaign.Id}",
                    "Client approved recommendation",
                    $"{campaignName} was approved by the client and can now move forward.",
                    $"/agent/campaigns/{campaign.Id}",
                    "success"));
            }
            else if (campaign.Status == CampaignStatuses.BookingInProgress)
            {
                items.Add(BuildItem(
                    $"agent-booking-{campaign.Id}",
                    "Supplier booking in progress",
                    $"{campaignName} is now in booking and launch preparation.",
                    $"/agent/campaigns/{campaign.Id}",
                    "info"));
            }
            else if (stage == "planning_ready")
            {
                items.Add(BuildItem($"agent-planning-ready-{campaign.Id}", "Campaign ready for recommendation", $"{campaignName} can now move into recommendation planning.", $"/agent/campaigns/{campaign.Id}", "info"));
            }
            else if (stage == "agent_review")
            {
                if (HasClientFeedback(latestRecommendation))
                {
                    items.Add(BuildItem(
                        $"agent-client-revision-{campaign.Id}",
                        "Client requested recommendation changes",
                        $"{campaignName} came back with client notes and needs a refreshed proposal set.",
                        $"/agent/campaigns/{campaign.Id}",
                        "warning"));
                }
                else
                {
                    items.Add(BuildItem($"agent-review-{campaign.Id}", "Recommendation needs strategist review", $"{campaignName} has checks or flags that need your attention.", $"/agent/campaigns/{campaign.Id}", "warning"));
                }
            }
            else if (stage == "waiting_on_client")
            {
                items.Add(BuildItem($"agent-client-wait-{campaign.Id}", "Waiting on client feedback", $"{campaignName} is with the client for approval or revisions.", $"/agent/campaigns/{campaign.Id}", "info"));
            }
            else if (stage == "newly_paid" || stage == "brief_waiting")
            {
                items.Add(BuildItem($"agent-brief-{campaign.Id}", "Campaign entered the strategist queue", $"{campaignName} is newly paid and moving through intake.", $"/agent/campaigns/{campaign.Id}", "info"));
            }
        }

        return items.Take(6).ToArray();
    }

    private async Task<NotificationSummaryItemResponse[]> BuildCreativeNotificationsAsync(CancellationToken cancellationToken)
    {
        return await BuildAgentNotificationsAsync(Guid.Empty, true, cancellationToken);
    }

    private async Task<NotificationSummaryItemResponse[]> BuildAdminNotificationsAsync(CancellationToken cancellationToken)
    {
        var dashboard = await _adminDashboardService.GetDashboardAsync(cancellationToken);

        var alertItems = dashboard.Alerts
            .Select((alert, index) => BuildItem(
                $"admin-alert-{index}-{alert.Title}",
                alert.Title,
                alert.Context,
                "/admin/health",
                alert.Severity.Contains("critical", StringComparison.OrdinalIgnoreCase) ? "warning" : "info"));

        var pricingItems = dashboard.HealthIssues
            .Where(item => item.Issue.Contains("pricing", StringComparison.OrdinalIgnoreCase) || item.Issue.Contains("inventory", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .Select(item => BuildItem(
                $"admin-pricing-{item.OutletCode}",
                $"{item.OutletName} needs pricing attention",
                item.SuggestedFix,
                $"/admin/pricing?outlet={Uri.EscapeDataString(item.OutletCode)}",
                "warning"));

        var items = alertItems.Concat(pricingItems).ToList();
        if (dashboard.Monitoring.WaitingOnClientCount > 0)
        {
            items.Add(BuildItem(
                "admin-waiting-on-client",
                "Campaigns are waiting on client approval",
                $"{dashboard.Monitoring.WaitingOnClientCount} recommendation set(s) are currently with clients.",
                "/admin/monitoring",
                "info"));
        }

        return items.Take(6).ToArray();
    }

    private static bool HasClientFeedback(CampaignRecommendation? recommendation)
    {
        return recommendation is not null
            && !string.IsNullOrWhiteSpace(recommendation.Rationale)
            && recommendation.Rationale.Contains("Client feedback:", StringComparison.OrdinalIgnoreCase);
    }

    private static NotificationSummaryItemResponse BuildItem(string id, string title, string description, string href, string tone)
    {
        return new NotificationSummaryItemResponse
        {
            Id = id,
            Title = title,
            Description = description,
            Href = href,
            Tone = tone,
            IsRead = false
        };
    }
}

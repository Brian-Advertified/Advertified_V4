using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class LeadOpsInboxService : ILeadOpsInboxService
{
    private static readonly TimeSpan NewInboundWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan UnassignedAlertWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NoRecentActivityWindow = TimeSpan.FromHours(48);

    private readonly AppDbContext _db;
    private readonly IAgentCampaignOwnershipService _ownershipService;

    public LeadOpsInboxService(AppDbContext db, IAgentCampaignOwnershipService ownershipService)
    {
        _db = db;
        _ownershipService = ownershipService;
    }

    public Task<LeadOpsInboxResponse> BuildAsync(UserAccount currentUser, CancellationToken cancellationToken)
    {
        return BuildInternalAsync(currentUser, includeAllCampaigns: currentUser.Role != UserRole.Agent, cancellationToken);
    }

    public Task<LeadOpsInboxResponse> BuildSystemSnapshotAsync(CancellationToken cancellationToken)
    {
        return BuildInternalAsync(currentUser: null, includeAllCampaigns: true, cancellationToken);
    }

    private async Task<LeadOpsInboxResponse> BuildInternalAsync(UserAccount? currentUser, bool includeAllCampaigns, CancellationToken cancellationToken)
    {
        var currentUserId = currentUser?.Id ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        var leadActionsQuery = _db.LeadActions
            .AsNoTracking()
            .Include(x => x.Lead)
            .Include(x => x.AssignedAgentUser)
            .Where(x => x.Status == "open");

        if (currentUser?.Role == UserRole.Agent)
        {
            leadActionsQuery = leadActionsQuery.Where(x => x.AssignedAgentUserId == currentUserId || x.AssignedAgentUserId == null);
        }

        var leadActionItems = await leadActionsQuery
            .OrderByDescending(x => x.Priority == "high")
            .ThenByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        IQueryable<Campaign> campaignsQuery = _db.Campaigns;
        if (!includeAllCampaigns && currentUser is not null)
        {
            campaignsQuery = _ownershipService.ApplyReadableScope(campaignsQuery, currentUser);
        }

        var campaigns = await campaignsQuery
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.ProspectDispositionStatus != ProspectDispositionStatuses.Closed)
            .Include(x => x.ProspectLead)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(150)
            .ToListAsync(cancellationToken);

        var items = new List<LeadOpsInboxItemResponse>();

        foreach (var action in leadActionItems)
        {
            var createdAt = new DateTimeOffset(action.CreatedAt, TimeSpan.Zero);
            items.Add(new LeadOpsInboxItemResponse
            {
                Id = $"lead-action:{action.Id}",
                ItemType = "open_lead_action",
                ItemLabel = "Open lead action",
                LeadId = action.LeadId,
                LeadActionId = action.Id,
                Title = action.Title,
                Subtitle = $"{action.Lead.Name} | {action.Lead.Location}",
                Description = action.Description,
                UnifiedStatus = LeadOpsPolicy.ResolveUnifiedLifecycleStage(null, false, false, true),
                AssignedAgentUserId = action.AssignedAgentUserId,
                AssignedAgentName = action.AssignedAgentUser?.FullName,
                IsAssignedToCurrentUser = action.AssignedAgentUserId == currentUserId,
                IsUnassigned = action.AssignedAgentUserId is null,
                IsUrgent = string.Equals(action.Priority, "high", StringComparison.OrdinalIgnoreCase) || action.AssignedAgentUserId is null,
                RoutePath = $"/agent/lead-intelligence?leadId={action.LeadId}",
                RouteLabel = "Open lead",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        }

        foreach (var campaign in campaigns)
        {
            if (campaign.ProspectLead is null || campaign.PackageOrder is null)
            {
                continue;
            }

            var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(campaign);
            var isProspect = ProspectCampaignPolicy.IsProspectiveCampaign(campaign);
            var createdAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero);
            var updatedAt = new DateTimeOffset(campaign.UpdatedAt, TimeSpan.Zero);
            var latestActivityAt = campaign.ProspectLead.LastContactedAt.HasValue
                ? new DateTimeOffset(
                    campaign.ProspectLead.LastContactedAt.Value > campaign.UpdatedAt
                        ? campaign.ProspectLead.LastContactedAt.Value
                        : campaign.UpdatedAt,
                    TimeSpan.Zero)
                : updatedAt;
            var unifiedStatus = LeadOpsPolicy.ResolveUnifiedLifecycleStage(
                campaign,
                hasProspect: true,
                hasHumanEngagement: campaign.ProspectLead.LastContactedAt.HasValue,
                hasOpenActions: false);
            var title = string.IsNullOrWhiteSpace(campaign.CampaignName)
                ? $"{campaign.PackageBand.Name} campaign"
                : campaign.CampaignName.Trim();
            var subtitle = $"{campaign.ProspectLead.FullName} | {campaign.ProspectLead.Email}";
            var routePath = $"/agent/campaigns/{campaign.Id}";
            var routeLabel = "Open campaign";
            var nextAction = CampaignWorkflowPolicy.GetAgentNextAction(campaign, queueStage, currentUserId);

            if (isProspect && now - createdAt <= NewInboundWindow)
            {
                items.Add(BuildCampaignItem(campaign, currentUserId, "new_inbound_prospect", "New inbound prospect", title, subtitle, nextAction, unifiedStatus, createdAt, updatedAt, routePath, routeLabel, campaign.ProspectLead.SlaDueAt));
            }

            if (isProspect && campaign.AssignedAgentUserId is null)
            {
                items.Add(BuildCampaignItem(campaign, currentUserId, "unassigned_prospect", "Unassigned prospect", title, subtitle, "Claim this prospect so follow-up work has a clear owner.", unifiedStatus, createdAt, updatedAt, routePath, routeLabel, campaign.ProspectLead.SlaDueAt, forceUrgent: now - updatedAt >= UnassignedAlertWindow));
            }

            if (queueStage == QueueStages.WaitingOnClient)
            {
                items.Add(BuildCampaignItem(campaign, currentUserId, "awaiting_client_response", "Awaiting client", title, subtitle, nextAction, unifiedStatus, createdAt, updatedAt, routePath, routeLabel));
            }

            if (isProspect && now - latestActivityAt >= NoRecentActivityWindow)
            {
                items.Add(BuildCampaignItem(campaign, currentUserId, "prospect_no_recent_activity", "No recent activity", title, subtitle, $"No recent contact or campaign update for {(int)Math.Floor((now - latestActivityAt).TotalHours)} hours.", unifiedStatus, createdAt, updatedAt, routePath, routeLabel, forceUrgent: true));
            }

            if (isProspect && campaign.ProspectLead.NextFollowUpAt.HasValue && campaign.ProspectLead.NextFollowUpAt.Value <= now.UtcDateTime)
            {
                items.Add(BuildCampaignItem(campaign, currentUserId, "overdue_follow_up", "Overdue follow-up", title, subtitle, "The next follow-up time has passed. This prospect needs outreach now.", unifiedStatus, createdAt, updatedAt, routePath, routeLabel, campaign.ProspectLead.NextFollowUpAt, forceUrgent: true));
            }
        }

        var orderedItems = items
            .OrderByDescending(x => x.IsUrgent)
            .ThenByDescending(x => x.IsAssignedToCurrentUser)
            .ThenByDescending(x => x.IsUnassigned)
            .ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(200)
            .ToArray();

        return new LeadOpsInboxResponse
        {
            TotalItems = orderedItems.Length,
            UrgentCount = orderedItems.Count(x => x.IsUrgent),
            AssignedToMeCount = orderedItems.Count(x => x.IsAssignedToCurrentUser),
            UnassignedCount = orderedItems.Count(x => x.IsUnassigned),
            NewInboundProspectsCount = orderedItems.Count(x => x.ItemType == "new_inbound_prospect"),
            UnassignedProspectsCount = orderedItems.Count(x => x.ItemType == "unassigned_prospect"),
            OpenLeadActionsCount = orderedItems.Count(x => x.ItemType == "open_lead_action"),
            NoRecentActivityCount = orderedItems.Count(x => x.ItemType == "prospect_no_recent_activity"),
            AwaitingClientResponsesCount = orderedItems.Count(x => x.ItemType == "awaiting_client_response"),
            OverdueFollowUpsCount = orderedItems.Count(x => x.ItemType == "overdue_follow_up"),
            Items = orderedItems
        };
    }

    private static LeadOpsInboxItemResponse BuildCampaignItem(
        Campaign campaign,
        Guid currentUserId,
        string itemType,
        string itemLabel,
        string title,
        string subtitle,
        string description,
        string unifiedStatus,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string routePath,
        string routeLabel,
        DateTime? dueAt = null,
        bool forceUrgent = false)
    {
        return new LeadOpsInboxItemResponse
        {
            Id = $"{itemType}:{campaign.Id}",
            ItemType = itemType,
            ItemLabel = itemLabel,
            CampaignId = campaign.Id,
            ProspectLeadId = campaign.ProspectLeadId,
            Title = title,
            Subtitle = subtitle,
            Description = description,
            UnifiedStatus = unifiedStatus,
            AssignedAgentUserId = campaign.AssignedAgentUserId,
            AssignedAgentName = campaign.AssignedAgentUser?.FullName,
            IsAssignedToCurrentUser = campaign.AssignedAgentUserId == currentUserId,
            IsUnassigned = campaign.AssignedAgentUserId is null,
            IsUrgent = forceUrgent || campaign.AssignedAgentUserId is null,
            RoutePath = routePath,
            RouteLabel = routeLabel,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DueAt = dueAt.HasValue ? new DateTimeOffset(dueAt.Value, TimeSpan.Zero) : null
        };
    }
}

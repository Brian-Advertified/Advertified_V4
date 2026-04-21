using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class LeadOpsCoverageService : ILeadOpsCoverageService
{
    private readonly AppDbContext _db;
    private readonly IAgentCampaignOwnershipService _ownershipService;

    public LeadOpsCoverageService(AppDbContext db, IAgentCampaignOwnershipService ownershipService)
    {
        _db = db;
        _ownershipService = ownershipService;
    }

    public Task<LeadOpsCoverageResponse> BuildAsync(UserAccount currentUser, CancellationToken cancellationToken)
    {
        return BuildInternalAsync(currentUser, includeAllLeads: currentUser.Role != UserRole.Agent, cancellationToken);
    }

    public Task<LeadOpsCoverageResponse> BuildSystemSnapshotAsync(CancellationToken cancellationToken)
    {
        return BuildInternalAsync(currentUser: null, includeAllLeads: true, cancellationToken);
    }

    private async Task<LeadOpsCoverageResponse> BuildInternalAsync(UserAccount? currentUser, bool includeAllLeads, CancellationToken cancellationToken)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var currentUserId = currentUser?.Id ?? Guid.Empty;

        IQueryable<Lead> leadsQuery = _db.Leads.AsNoTracking();
        if (!includeAllLeads && currentUser is not null)
        {
            leadsQuery = ApplyOperationsLeadScope(leadsQuery, currentUser);
        }

        var scopedLeadIds = await leadsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        IQueryable<Campaign> campaignsQuery = _db.Campaigns;
        if (!includeAllLeads && currentUser is not null && currentUser.Role == UserRole.Agent)
        {
            campaignsQuery = _ownershipService.ApplyReadableScope(campaignsQuery, currentUser);
        }

        var campaigns = await campaignsQuery
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.ProspectLead)
                .ThenInclude(x => x!.OwnerAgentUser)
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.CampaignBrief)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var campaignsByLeadId = new Dictionary<int, List<Campaign>>();
        foreach (var campaign in campaigns)
        {
            if (!TryResolveSourceLeadId(campaign, out var sourceLeadId))
            {
                continue;
            }

            if (!campaignsByLeadId.TryGetValue(sourceLeadId, out var bucket))
            {
                bucket = new List<Campaign>();
                campaignsByLeadId[sourceLeadId] = bucket;
            }

            bucket.Add(campaign);
        }

        IQueryable<ProspectLead> prospectsQuery = _db.ProspectLeads
            .AsNoTracking()
            .Include(x => x.OwnerAgentUser)
            .Where(x => x.SourceLeadId.HasValue);

        if (!includeAllLeads && currentUser is not null && currentUser.Role == UserRole.Agent)
        {
            prospectsQuery = prospectsQuery.Where(x => x.OwnerAgentUserId == currentUserId || x.OwnerAgentUserId == null);
        }

        var prospects = await prospectsQuery
            .OrderByDescending(x => x.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var prospectLeadIds = prospects
            .Select(x => x.SourceLeadId!.Value)
            .Concat(campaignsByLeadId.Keys)
            .Distinct()
            .ToArray();

        var leadIds = scopedLeadIds
            .Concat(prospectLeadIds)
            .Distinct()
            .ToArray();

        var leads = await _db.Leads
            .AsNoTracking()
            .Include(x => x.OwnerAgentUser)
            .Where(x => leadIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var actions = await _db.LeadActions
            .AsNoTracking()
            .Include(x => x.AssignedAgentUser)
            .Where(x => leadIds.Contains(x.LeadId))
            .ToListAsync(cancellationToken);

        var interactions = await _db.LeadInteractions
            .AsNoTracking()
            .Where(x => leadIds.Contains(x.LeadId))
            .ToListAsync(cancellationToken);

        var prospectsByLeadId = prospects
            .Where(x => x.SourceLeadId.HasValue)
            .GroupBy(x => x.SourceLeadId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.UpdatedAt).ToList());

        var actionsByLeadId = actions
            .GroupBy(x => x.LeadId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var interactionsByLeadId = interactions
            .GroupBy(x => x.LeadId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.CreatedAt).ToList());

        var items = leads
            .Select(lead =>
            {
                var leadActions = actionsByLeadId.GetValueOrDefault(lead.Id) ?? new List<LeadAction>();
                var openLeadActions = leadActions
                    .Where(x => string.Equals(x.Status, "open", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var leadInteractions = interactionsByLeadId.GetValueOrDefault(lead.Id) ?? new List<LeadInteraction>();
                var leadProspects = prospectsByLeadId.GetValueOrDefault(lead.Id) ?? new List<ProspectLead>();
                var leadCampaigns = campaignsByLeadId.GetValueOrDefault(lead.Id) ?? new List<Campaign>();
                var preferredCampaign = SelectPreferredCampaign(leadCampaigns);
                var winningCampaign = leadCampaigns
                    .Where(x => string.Equals(x.PackageOrder?.PaymentStatus, CampaignStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.PackageOrder?.PurchasedAt ?? x.UpdatedAt)
                    .FirstOrDefault();
                var owner = ResolveOwner(lead, leadProspects, leadCampaigns, openLeadActions);
                var firstContactedAt = ResolveFirstContactedAt(lead, leadInteractions);
                var lastContactedAt = ResolveLastContactedAt(lead, leadInteractions, leadProspects);
                var nextFollowUpAt = ResolveNextFollowUpAt(lead, leadProspects, preferredCampaign);
                var slaDueAt = ResolveSlaDueAt(lead, leadProspects, preferredCampaign);
                var hasBeenContacted = firstContactedAt.HasValue || lastContactedAt.HasValue;
                var hasHumanEngagement = LeadOpsPolicy.HasHumanEngagement(leadInteractions);
                var nextAction = ResolveNextAction(lead, preferredCampaign, leadProspects, openLeadActions, hasBeenContacted, currentUserId);
                var unifiedStatus = LeadOpsPolicy.ResolveUnifiedLifecycleStage(
                    preferredCampaign ?? winningCampaign,
                    hasProspect: leadProspects.Count > 0 || leadCampaigns.Count > 0,
                    hasHumanEngagement,
                    hasOpenActions: openLeadActions.Count > 0);
                var assignmentStatus = owner.UserId.HasValue
                    ? owner.Resolution == "multiple_action_owners" ? "ambiguous" : "assigned"
                    : "unassigned";
                var contactStatus = hasBeenContacted ? "contact_started" : "not_contacted";
                var attentionReasons = BuildAttentionReasons(preferredCampaign, openLeadActions, hasBeenContacted, nextFollowUpAt, slaDueAt);
                var priority = DeterminePriority(attentionReasons, hasBeenContacted, owner.UserId.HasValue);
                var routePath = preferredCampaign is not null
                    ? $"/agent/campaigns/{preferredCampaign.Id}"
                    : $"/agent/lead-intelligence?leadId={lead.Id}";

                return new LeadOpsCoverageItemResponse
                {
                    RecordKey = preferredCampaign is not null
                        ? $"campaign:{preferredCampaign.Id}"
                        : leadProspects.FirstOrDefault() is { } prospect
                            ? $"prospect:{prospect.Id}"
                            : $"lead:{lead.Id}",
                    LeadId = lead.Id,
                    LeadName = lead.Name,
                    Location = lead.Location,
                    Category = lead.Category,
                    Source = string.IsNullOrWhiteSpace(lead.Source) ? "unknown" : lead.Source.Trim(),
                    SourceReference = lead.SourceReference,
                    UnifiedStatus = unifiedStatus,
                    OwnerAgentUserId = owner.UserId,
                    OwnerAgentName = owner.Name,
                    OwnerResolution = owner.Resolution,
                    AssignmentStatus = assignmentStatus,
                    HasBeenContacted = hasBeenContacted,
                    FirstContactedAt = firstContactedAt,
                    ContactStatus = contactStatus,
                    LastContactedAt = lastContactedAt,
                    NextAction = nextAction.Label,
                    NextActionDueAt = nextAction.DueAt,
                    NextFollowUpAt = nextFollowUpAt,
                    SlaDueAt = slaDueAt,
                    Priority = priority,
                    AttentionReasons = attentionReasons,
                    OpenLeadActionCount = openLeadActions.Count,
                    HasProspect = leadProspects.Count > 0 || leadCampaigns.Count > 0,
                    ProspectLeadId = leadProspects.FirstOrDefault()?.Id ?? preferredCampaign?.ProspectLeadId,
                    ActiveCampaignId = preferredCampaign?.Id,
                    WonCampaignId = winningCampaign?.Id,
                    ConvertedToSale = winningCampaign is not null,
                    LastOutcome = ResolveLastOutcome(lead, leadProspects, preferredCampaign, winningCampaign),
                    RoutePath = routePath
                };
            })
            .OrderByDescending(x => x.ConvertedToSale)
            .ThenByDescending(x => x.HasProspect)
            .ThenBy(x => x.OwnerAgentName is null)
            .ThenByDescending(x => x.LastContactedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(x => x.LeadId)
            .ToArray();

        var sources = items
            .GroupBy(x => x.Source, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LeadOpsCoverageSourceResponse
            {
                Source = group.Key,
                LeadCount = group.Count(),
                ProspectCount = group.Count(x => x.HasProspect),
                WonCount = group.Count(x => x.ConvertedToSale)
            })
            .OrderByDescending(x => x.LeadCount)
            .ThenBy(x => x.Source)
            .ToArray();

        var totalLeadCount = items.Length;
        var prospectLeadCount = items.Count(x => x.HasProspect);
        var wonLeadCount = items.Count(x => x.ConvertedToSale);

        return new LeadOpsCoverageResponse
        {
            GeneratedAtUtc = generatedAtUtc,
            TotalLeadCount = totalLeadCount,
            OwnedLeadCount = items.Count(x => x.OwnerAgentUserId.HasValue),
            UnownedLeadCount = items.Count(x => !x.OwnerAgentUserId.HasValue && x.OwnerResolution != "multiple_action_owners"),
            AmbiguousOwnerCount = items.Count(x => x.OwnerResolution == "multiple_action_owners"),
            UncontactedLeadCount = items.Count(x => !x.HasBeenContacted),
            LeadsWithNextActionCount = items.Count(x => !string.IsNullOrWhiteSpace(x.NextAction)),
            ProspectLeadCount = prospectLeadCount,
            ActiveDealCount = items.Count(x => x.ActiveCampaignId.HasValue),
            WonLeadCount = wonLeadCount,
            LeadToProspectRatePercent = totalLeadCount == 0 ? 0m : Math.Round(prospectLeadCount * 100m / totalLeadCount, 1),
            LeadToSaleRatePercent = totalLeadCount == 0 ? 0m : Math.Round(wonLeadCount * 100m / totalLeadCount, 1),
            Sources = sources,
            Items = items
        };
    }

    private IQueryable<Lead> ApplyOperationsLeadScope(IQueryable<Lead> query, UserAccount currentUser)
    {
        if (currentUser.Role != UserRole.Agent)
        {
            return query;
        }

        var currentUserId = currentUser.Id;
        return query.Where(lead =>
            lead.OwnerAgentUserId == currentUserId
            || lead.OwnerAgentUserId == null
            || _db.LeadActions.Any(action =>
                action.LeadId == lead.Id
                && (action.AssignedAgentUserId == currentUserId || action.AssignedAgentUserId == null))
            || !_db.LeadActions.Any(action => action.LeadId == lead.Id));
    }

    private static bool TryResolveSourceLeadId(Campaign campaign, out int sourceLeadId)
    {
        if (campaign.ProspectLead?.SourceLeadId is int linkedLeadId && linkedLeadId > 0)
        {
            sourceLeadId = linkedLeadId;
            return true;
        }

        return LeadOutreachCampaignSupport.TryGetSourceLeadId(campaign, out sourceLeadId);
    }

    private static Campaign? SelectPreferredCampaign(IEnumerable<Campaign> campaigns)
    {
        return campaigns
            .OrderByDescending(x => !string.Equals(x.PackageOrder?.PaymentStatus, CampaignStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.ProspectDispositionStatus == ProspectDispositionStatuses.Open)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();
    }

    private static DateTimeOffset? ResolveFirstContactedAt(Lead lead, IReadOnlyCollection<LeadInteraction> interactions)
    {
        var interactionAt = interactions.Count == 0
            ? null
            : interactions.Min(x => (DateTime?)x.CreatedAt);
        var first = new[] { lead.FirstContactedAt, interactionAt }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty()
            .Min();

        return first == default ? null : new DateTimeOffset(first, TimeSpan.Zero);
    }

    private static DateTimeOffset? ResolveLastContactedAt(
        Lead lead,
        IReadOnlyCollection<LeadInteraction> interactions,
        IReadOnlyCollection<ProspectLead> prospects)
    {
        var interactionContactAt = interactions.Count == 0
            ? null
            : interactions.Max(x => (DateTime?)x.CreatedAt);
        var prospectContactAt = prospects
            .Where(x => x.LastContactedAt.HasValue)
            .Select(x => x.LastContactedAt)
            .DefaultIfEmpty()
            .Max();

        var latest = new[] { lead.LastContactedAt, interactionContactAt, prospectContactAt }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty()
            .Max();

        return latest == default ? null : new DateTimeOffset(latest, TimeSpan.Zero);
    }

    private static DateTimeOffset? ResolveNextFollowUpAt(Lead lead, IReadOnlyCollection<ProspectLead> prospects, Campaign? preferredCampaign)
    {
        var prospectFollowUpAt = preferredCampaign?.ProspectLead?.NextFollowUpAt
            ?? prospects.Where(x => x.NextFollowUpAt.HasValue).Select(x => x.NextFollowUpAt).OrderBy(x => x).FirstOrDefault();
        var value = prospectFollowUpAt ?? lead.NextFollowUpAt;
        return value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null;
    }

    private static DateTimeOffset? ResolveSlaDueAt(Lead lead, IReadOnlyCollection<ProspectLead> prospects, Campaign? preferredCampaign)
    {
        var prospectSlaAt = preferredCampaign?.ProspectLead?.SlaDueAt
            ?? prospects.Where(x => x.SlaDueAt.HasValue).Select(x => x.SlaDueAt).OrderBy(x => x).FirstOrDefault();
        var value = prospectSlaAt ?? lead.SlaDueAt;
        return value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null;
    }

    private static LeadOpsOwnerResolution ResolveOwner(
        Lead lead,
        IReadOnlyCollection<ProspectLead> prospects,
        IReadOnlyCollection<Campaign> campaigns,
        IReadOnlyCollection<LeadAction> openLeadActions)
    {
        if (lead.OwnerAgentUserId.HasValue)
        {
            return new LeadOpsOwnerResolution(lead.OwnerAgentUserId, lead.OwnerAgentUser?.FullName, "lead_owner");
        }

        var campaignOwner = campaigns
            .Where(x => x.AssignedAgentUserId.HasValue)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new LeadOpsOwnerResolution(x.AssignedAgentUserId, x.AssignedAgentUser?.FullName, "campaign_owner"))
            .FirstOrDefault();
        if (campaignOwner is not null)
        {
            return campaignOwner;
        }

        var prospectOwner = prospects
            .Where(x => x.OwnerAgentUserId.HasValue)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new LeadOpsOwnerResolution(x.OwnerAgentUserId, x.OwnerAgentUser?.FullName, "prospect_owner"))
            .FirstOrDefault();
        if (prospectOwner is not null)
        {
            return prospectOwner;
        }

        var distinctActionOwners = openLeadActions
            .Where(x => x.AssignedAgentUserId.HasValue)
            .GroupBy(x => x.AssignedAgentUserId)
            .Select(group => group.OrderByDescending(x => x.AssignedAt ?? x.CreatedAt).First())
            .ToList();
        if (distinctActionOwners.Count == 1)
        {
            var owner = distinctActionOwners[0];
            return new LeadOpsOwnerResolution(owner.AssignedAgentUserId, owner.AssignedAgentUser?.FullName, "lead_action_owner");
        }

        if (distinctActionOwners.Count > 1)
        {
            return new LeadOpsOwnerResolution(null, "Multiple owners", "multiple_action_owners");
        }

        return new LeadOpsOwnerResolution(null, null, "unassigned");
    }

    private static LeadOpsNextAction ResolveNextAction(
        Lead lead,
        Campaign? preferredCampaign,
        IReadOnlyCollection<ProspectLead> prospects,
        IReadOnlyCollection<LeadAction> openLeadActions,
        bool hasBeenContacted,
        Guid currentUserId)
    {
        if (preferredCampaign is not null)
        {
            var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(preferredCampaign);
            return new LeadOpsNextAction(
                CampaignWorkflowPolicy.GetAgentNextAction(preferredCampaign, queueStage, currentUserId),
                preferredCampaign.ProspectLead?.NextFollowUpAt.HasValue == true
                    ? new DateTimeOffset(preferredCampaign.ProspectLead.NextFollowUpAt.Value, TimeSpan.Zero)
                    : null);
        }

        var primaryAction = openLeadActions
            .OrderByDescending(x => string.Equals(x.Priority, "high", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefault();
        if (primaryAction is not null)
        {
            return new LeadOpsNextAction(primaryAction.Title, null);
        }

        var nextFollowUpAt = prospects
            .Where(x => x.NextFollowUpAt.HasValue)
            .Select(x => x.NextFollowUpAt)
            .OrderBy(x => x)
            .FirstOrDefault();
        if (nextFollowUpAt.HasValue)
        {
            return new LeadOpsNextAction("Follow up with the prospect.", new DateTimeOffset(nextFollowUpAt.Value, TimeSpan.Zero));
        }

        if (lead.NextFollowUpAt.HasValue)
        {
            return new LeadOpsNextAction("Follow up with this lead.", new DateTimeOffset(lead.NextFollowUpAt.Value, TimeSpan.Zero));
        }

        if (!hasBeenContacted)
        {
            return new LeadOpsNextAction($"Make first contact with {lead.Name}.", null);
        }

        return new LeadOpsNextAction("Review qualification and decide whether to open a prospect.", null);
    }

    private static IReadOnlyList<string> BuildAttentionReasons(
        Campaign? preferredCampaign,
        IReadOnlyCollection<LeadAction> openLeadActions,
        bool hasBeenContacted,
        DateTimeOffset? nextFollowUpAt,
        DateTimeOffset? slaDueAt)
    {
        var reasons = new List<string>();
        var now = DateTimeOffset.UtcNow;

        if (preferredCampaign is not null && preferredCampaign.AssignedAgentUserId is null)
        {
            reasons.Add("unassigned");
        }

        if (!hasBeenContacted)
        {
            reasons.Add("first_contact_needed");
        }

        if (openLeadActions.Any(x => string.Equals(x.Priority, "high", StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add("high_priority_action");
        }

        if (nextFollowUpAt.HasValue && nextFollowUpAt.Value <= now)
        {
            reasons.Add("follow_up_overdue");
        }

        if (slaDueAt.HasValue && slaDueAt.Value <= now)
        {
            reasons.Add("sla_overdue");
        }

        if (preferredCampaign is not null && CampaignWorkflowPolicy.ResolveAgentQueueStage(preferredCampaign) == QueueStages.WaitingOnClient)
        {
            reasons.Add("awaiting_client");
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string DeterminePriority(IReadOnlyCollection<string> attentionReasons, bool hasBeenContacted, bool hasOwner)
    {
        if (attentionReasons.Contains("follow_up_overdue", StringComparer.OrdinalIgnoreCase)
            || attentionReasons.Contains("sla_overdue", StringComparer.OrdinalIgnoreCase)
            || attentionReasons.Contains("high_priority_action", StringComparer.OrdinalIgnoreCase))
        {
            return "high";
        }

        if (!hasBeenContacted || !hasOwner)
        {
            return "high";
        }

        return "normal";
    }

    private static string? ResolveLastOutcome(Lead lead, IReadOnlyCollection<ProspectLead> prospects, Campaign? preferredCampaign, Campaign? winningCampaign)
    {
        if (winningCampaign is not null)
        {
            return "Won";
        }

        if (preferredCampaign is not null && ProspectCampaignPolicy.IsClosed(preferredCampaign))
        {
            return string.IsNullOrWhiteSpace(preferredCampaign.ProspectDispositionReason)
                ? "Lost"
                : $"Lost: {preferredCampaign.ProspectDispositionReason}";
        }

        var prospectOutcome = prospects
            .Where(x => !string.IsNullOrWhiteSpace(x.LastOutcome))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.LastOutcome)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(prospectOutcome) ? lead.LastOutcome : prospectOutcome;
    }

    private sealed record LeadOpsOwnerResolution(Guid? UserId, string? Name, string Resolution);

    private sealed record LeadOpsNextAction(string Label, DateTimeOffset? DueAt);
}

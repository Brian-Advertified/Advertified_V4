using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class LeadOpsStateService : ILeadOpsStateService
{
    private readonly AppDbContext _db;

    public LeadOpsStateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task RefreshCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.ProspectLead)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return;
        }

        if (campaign.ProspectLeadId.HasValue)
        {
            await RefreshProspectAsync(campaign.ProspectLeadId.Value, cancellationToken);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshProspectAsync(Guid prospectLeadId, CancellationToken cancellationToken)
    {
        var prospect = await _db.ProspectLeads
            .Include(x => x.Campaigns)
                .ThenInclude(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == prospectLeadId, cancellationToken);
        if (prospect is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var preferredCampaign = prospect.Campaigns
            .OrderByDescending(x => x.AssignedAgentUserId.HasValue)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        var changed = false;
        if (preferredCampaign is not null && prospect.OwnerAgentUserId != preferredCampaign.AssignedAgentUserId)
        {
            prospect.OwnerAgentUserId = preferredCampaign.AssignedAgentUserId;
            changed = true;
        }

        if (changed)
        {
            prospect.UpdatedAt = now;
        }

        if (prospect.SourceLeadId.HasValue)
        {
            await RefreshLeadAsync(prospect.SourceLeadId.Value, cancellationToken);
            return;
        }

        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RefreshLeadAsync(int leadId, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .Include(x => x.Actions)
            .Include(x => x.Interactions)
            .FirstOrDefaultAsync(x => x.Id == leadId, cancellationToken);
        if (lead is null)
        {
            return;
        }

        var prospects = await _db.ProspectLeads
            .Include(x => x.Campaigns)
                .ThenInclude(x => x.PackageOrder)
            .Where(x => x.SourceLeadId == leadId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var openActions = lead.Actions
            .Where(x => string.Equals(x.Status, "open", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ownerUserId = ResolveOwnerUserId(prospects, openActions);
        var interactionMoments = lead.Interactions
            .Select(x => x.CreatedAt)
            .OrderBy(x => x)
            .ToList();
        var firstInteractionAt = interactionMoments.FirstOrDefault();
        var lastInteractionAt = interactionMoments.LastOrDefault();
        var prospectLastContactedAt = prospects
            .Where(x => x.LastContactedAt.HasValue)
            .Select(x => x.LastContactedAt!.Value)
            .OrderByDescending(x => x)
            .FirstOrDefault();
        var nextFollowUpAt = prospects
            .Where(x => x.NextFollowUpAt.HasValue)
            .Select(x => x.NextFollowUpAt!.Value)
            .OrderBy(x => x)
            .FirstOrDefault();
        var slaDueAt = prospects
            .Where(x => x.SlaDueAt.HasValue)
            .Select(x => x.SlaDueAt!.Value)
            .OrderBy(x => x)
            .FirstOrDefault();
        var preferredOutcome = ResolveLastOutcome(prospects);

        lead.OwnerAgentUserId = ownerUserId;
        lead.FirstContactedAt = ResolveFirstContactedAt(lead.FirstContactedAt, firstInteractionAt, prospectLastContactedAt);
        lead.LastContactedAt = ResolveLastContactedAt(lead.LastContactedAt, lastInteractionAt, prospectLastContactedAt);
        lead.NextFollowUpAt = nextFollowUpAt == default ? lead.NextFollowUpAt : nextFollowUpAt;
        lead.SlaDueAt = slaDueAt == default ? lead.SlaDueAt : slaDueAt;
        lead.LastOutcome = string.IsNullOrWhiteSpace(preferredOutcome) ? lead.LastOutcome : preferredOutcome;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static Guid? ResolveOwnerUserId(IReadOnlyCollection<ProspectLead> prospects, IReadOnlyCollection<LeadAction> openActions)
    {
        var campaignOwner = prospects
            .SelectMany(x => x.Campaigns)
            .Where(x => x.AssignedAgentUserId.HasValue)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.AssignedAgentUserId)
            .FirstOrDefault();
        if (campaignOwner.HasValue)
        {
            return campaignOwner;
        }

        var prospectOwner = prospects
            .Where(x => x.OwnerAgentUserId.HasValue)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.OwnerAgentUserId)
            .FirstOrDefault();
        if (prospectOwner.HasValue)
        {
            return prospectOwner;
        }

        var distinctActionOwners = openActions
            .Where(x => x.AssignedAgentUserId.HasValue)
            .Select(x => x.AssignedAgentUserId!.Value)
            .Distinct()
            .ToArray();

        return distinctActionOwners.Length == 1
            ? distinctActionOwners[0]
            : null;
    }

    private static DateTime? ResolveFirstContactedAt(DateTime? currentValue, DateTime firstInteractionAt, DateTime prospectLastContactedAt)
    {
        var values = new[] { currentValue, firstInteractionAt == default ? null : firstInteractionAt, prospectLastContactedAt == default ? null : prospectLastContactedAt }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(x => x)
            .ToArray();

        return values.Length == 0 ? null : values[0];
    }

    private static DateTime? ResolveLastContactedAt(DateTime? currentValue, DateTime lastInteractionAt, DateTime prospectLastContactedAt)
    {
        var values = new[] { currentValue, lastInteractionAt == default ? null : lastInteractionAt, prospectLastContactedAt == default ? null : prospectLastContactedAt }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderByDescending(x => x)
            .ToArray();

        return values.Length == 0 ? null : values[0];
    }

    private static string? ResolveLastOutcome(IReadOnlyCollection<ProspectLead> prospects)
    {
        var wonCampaign = prospects
            .SelectMany(x => x.Campaigns)
            .FirstOrDefault(x => string.Equals(x.PackageOrder?.PaymentStatus, CampaignStatuses.Paid, StringComparison.OrdinalIgnoreCase));
        if (wonCampaign is not null)
        {
            return "Won";
        }

        var lostCampaign = prospects
            .SelectMany(x => x.Campaigns)
            .Where(ProspectCampaignPolicy.IsClosed)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();
        if (lostCampaign is not null)
        {
            return string.IsNullOrWhiteSpace(lostCampaign.ProspectDispositionReason)
                ? "Lost"
                : $"Lost: {lostCampaign.ProspectDispositionReason}";
        }

        return prospects
            .Where(x => !string.IsNullOrWhiteSpace(x.LastOutcome))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.LastOutcome)
            .FirstOrDefault();
    }
}

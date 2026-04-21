using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class LeadProposalConfidenceGateService : ILeadProposalConfidenceGateService
{
    private readonly AppDbContext _db;
    private readonly ILeadChannelDetectionService _leadChannelDetectionService;
    private readonly ILeadEnrichmentSnapshotService _leadEnrichmentSnapshotService;
    private readonly ILeadIndustryContextResolver _leadIndustryContextResolver;

    public LeadProposalConfidenceGateService(
        AppDbContext db,
        ILeadChannelDetectionService leadChannelDetectionService,
        ILeadEnrichmentSnapshotService leadEnrichmentSnapshotService,
        ILeadIndustryContextResolver leadIndustryContextResolver)
    {
        _db = db;
        _leadChannelDetectionService = leadChannelDetectionService;
        _leadEnrichmentSnapshotService = leadEnrichmentSnapshotService;
        _leadIndustryContextResolver = leadIndustryContextResolver;
    }

    public async Task EnsureCampaignReadyAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!LeadOutreachCampaignSupport.RequiresLeadConfidenceGate(campaign))
        {
            return;
        }

        if (!LeadOutreachCampaignSupport.TryGetSourceLeadId(campaign, out var sourceLeadId))
        {
            throw new InvalidOperationException(
                "Lead confidence gate could not be verified because this campaign is missing a source lead reference. Regenerate from Lead Intelligence first.");
        }

        var lead = await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sourceLeadId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Lead confidence gate could not be verified because the source lead was not found.");

        var latestSignal = await _db.Signals
            .AsNoTracking()
            .Where(x => x.LeadId == sourceLeadId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        IReadOnlyList<Data.Entities.LeadSignalEvidence> evidences;
        if (latestSignal is null)
        {
            evidences = Array.Empty<Data.Entities.LeadSignalEvidence>();
        }
        else
        {
            evidences = await _db.LeadSignalEvidences
                .AsNoTracking()
                .Where(x => x.SignalId == latestSignal.Id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        var industryContext = _leadIndustryContextResolver.ResolveFromCategory(lead.Category);
        var channelDetections = _leadChannelDetectionService.Detect(lead, latestSignal, evidences, industryContext.CanonicalIndustry);
        var enrichment = _leadEnrichmentSnapshotService.Build(lead, latestSignal, evidences, channelDetections, industryContext.CanonicalIndustry, industryContext);
        if (enrichment.ConfidenceGate.IsBlocked)
        {
            throw new InvalidOperationException(enrichment.ConfidenceGate.Message);
        }
    }
}

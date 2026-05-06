using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Advertified.App.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class CampaignBriefService : ICampaignBriefService
{
    private static readonly string[] AllowedModes = { "ai_assisted", "agent_assisted", "hybrid" };
    private readonly AppDbContext _db;
    private readonly SaveCampaignBriefRequestValidator _validator;
    private readonly ITemplatedEmailService _emailService;
    private readonly IAgentAreaRoutingService _agentAreaRoutingService;
    private readonly ILocationCatalogService _locationCatalogService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<CampaignBriefService> _logger;

    public CampaignBriefService(
        AppDbContext db,
        SaveCampaignBriefRequestValidator validator,
        ITemplatedEmailService emailService,
        IAgentAreaRoutingService agentAreaRoutingService,
        ILocationCatalogService locationCatalogService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<CampaignBriefService> logger)
    {
        _db = db;
        _validator = validator;
        _emailService = emailService;
        _agentAreaRoutingService = agentAreaRoutingService;
        _locationCatalogService = locationCatalogService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task SaveDraftAsync(Guid userId, Guid campaignId, SaveCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder))
        {
            throw new InvalidOperationException("Package must be active before the campaign brief can be completed.");
        }

        var now = DateTime.UtcNow;
        await UpsertBriefAsync(campaignId, request, now, cancellationToken);
        await UpsertDraftAsync(campaignId, request, now, cancellationToken);

        campaign.Status = CampaignStatuses.BriefInProgress;
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await TrySeedLocationCatalogAsync(request, cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaignId, "brief_saved", cancellationToken);
    }

    public async Task SaveAgentManagedAsync(
        Campaign campaign,
        SaveCampaignBriefRequest request,
        string planningMode,
        string? campaignName,
        bool submitBrief,
        CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        ValidatePlanningMode(planningMode);

        var now = DateTime.UtcNow;
        var brief = await UpsertBriefAsync(campaign.Id, request, now, cancellationToken);
        await UpsertDraftAsync(campaign.Id, request, now, cancellationToken);

        if (!string.IsNullOrWhiteSpace(campaignName))
        {
            campaign.CampaignName = campaignName.Trim();
        }

        campaign.PlanningMode = planningMode;
        campaign.AgentAssistanceRequested = planningMode is "agent_assisted" or "hybrid";
        if (submitBrief)
        {
            campaign.Status = CampaignStatuses.PlanningInProgress;
            brief.SubmittedAt ??= now;
        }
        else if (string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase))
        {
            campaign.Status = CampaignStatuses.BriefInProgress;
        }

        CampaignAiAccessPolicy.Apply(campaign, brief);
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        await TrySeedLocationCatalogAsync(request, cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaign.Id, submitBrief ? "brief_submitted" : "brief_saved", cancellationToken);
    }

    public async Task SaveProspectSubmissionAsync(
        Campaign campaign,
        SaveCampaignBriefRequest request,
        DateTime submittedAt,
        CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var brief = await UpsertBriefAsync(campaign.Id, request, submittedAt, cancellationToken);
        await UpsertDraftAsync(campaign.Id, request, submittedAt, cancellationToken);

        brief.SubmittedAt = submittedAt;
        campaign.Status = CampaignStatuses.BriefSubmitted;
        campaign.PlanningMode = string.IsNullOrWhiteSpace(campaign.PlanningMode) ? "hybrid" : campaign.PlanningMode;
        campaign.AgentAssistanceRequested = true;
        CampaignAiAccessPolicy.Apply(campaign, brief);
        campaign.UpdatedAt = submittedAt;

        await _db.SaveChangesAsync(cancellationToken);
        await TrySeedLocationCatalogAsync(request, cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaign.Id, "prospect_brief_submitted", cancellationToken);
    }

    public async Task SubmitAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder))
        {
            throw new InvalidOperationException("Package must be active before brief submission.");
        }

        var brief = await _db.CampaignBriefs
            .FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign brief not found.");
        var campaignUser = RequireCampaignUser(campaign);

        brief.SubmittedAt = DateTime.UtcNow;
        brief.UpdatedAt = DateTime.UtcNow;
        campaign.Status = CampaignStatuses.BriefSubmitted;
        CampaignAiAccessPolicy.Apply(campaign, brief);
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaignId, "brief_submitted", cancellationToken);

        try
        {
            await _emailService.SendAsync(
                "brief-submitted",
                campaignUser.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaignUser.FullName,
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["Budget"] = CurrencyFormatSupport.FormatZar(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send brief submitted email for campaign {CampaignId}.", campaign.Id);
        }
    }

    public async Task SetPlanningModeAsync(Guid userId, Guid campaignId, string planningMode, CancellationToken cancellationToken)
    {
        ValidatePlanningMode(planningMode);

        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (campaign.Status is not (CampaignStatuses.BriefSubmitted or CampaignStatuses.PlanningInProgress))
        {
            throw new InvalidOperationException("Planning mode can only be selected after brief submission.");
        }

        campaign.PlanningMode = planningMode;
        campaign.AgentAssistanceRequested = planningMode is "agent_assisted" or "hybrid";
        campaign.Status = CampaignStatuses.PlanningInProgress;
        CampaignAiAccessPolicy.Apply(campaign);
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CampaignBrief> UpsertBriefAsync(
        Guid campaignId,
        SaveCampaignBriefRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var brief = await _db.CampaignBriefs
            .FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken);

        if (brief is null)
        {
            brief = new CampaignBrief
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                CreatedAt = now
            };
            _db.CampaignBriefs.Add(brief);
        }

        CampaignBriefMapper.Apply(brief, request, now);
        return brief;
    }

    private async Task UpsertDraftAsync(
        Guid campaignId,
        SaveCampaignBriefRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var draftJson = JsonSerializer.Serialize(request);
        var draft = await _db.CampaignBriefDrafts
            .FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken);

        if (draft is null)
        {
            _db.CampaignBriefDrafts.Add(new CampaignBriefDraft
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                DraftJson = draftJson,
                SavedAt = now
            });
            return;
        }

        draft.DraftJson = draftJson;
        draft.SavedAt = now;
    }

    private static void ValidatePlanningMode(string planningMode)
    {
        if (!AllowedModes.Contains(planningMode))
        {
            throw new InvalidOperationException("Invalid planning mode.");
        }
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static UserAccount RequireCampaignUser(Campaign campaign)
    {
        return campaign.User
            ?? throw new InvalidOperationException("Campaign is missing its client account.");
    }

    private async Task TrySeedLocationCatalogAsync(SaveCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _locationCatalogService.SeedResolvedLocationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolved campaign location could not be added to the master location catalog.");
        }
    }
}

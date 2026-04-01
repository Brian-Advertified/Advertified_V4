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
using System.Globalization;

namespace Advertified.App.Services;

public sealed class CampaignBriefService : ICampaignBriefService
{
    private static readonly string[] AllowedModes = { "ai_assisted", "agent_assisted", "hybrid" };
    private readonly AppDbContext _db;
    private readonly SaveCampaignBriefRequestValidator _validator;
    private readonly ITemplatedEmailService _emailService;
    private readonly IAgentAreaRoutingService _agentAreaRoutingService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<CampaignBriefService> _logger;

    public CampaignBriefService(
        AppDbContext db,
        SaveCampaignBriefRequestValidator validator,
        ITemplatedEmailService emailService,
        IAgentAreaRoutingService agentAreaRoutingService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<CampaignBriefService> logger)
    {
        _db = db;
        _validator = validator;
        _emailService = emailService;
        _agentAreaRoutingService = agentAreaRoutingService;
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
        var draftJson = JsonSerializer.Serialize(request);
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

        MapBrief(brief, request, now);

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
        }
        else
        {
            draft.DraftJson = draftJson;
            draft.SavedAt = now;
        }

        campaign.Status = CampaignStatuses.BriefInProgress;
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaignId, "brief_saved", cancellationToken);
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

        brief.SubmittedAt = DateTime.UtcNow;
        brief.UpdatedAt = DateTime.UtcNow;
        campaign.Status = CampaignStatuses.BriefSubmitted;
        campaign.AiUnlocked = true;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaignId, "brief_submitted", cancellationToken);

        try
        {
            await _emailService.SendAsync(
                "brief-submitted",
                campaign.User.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.User.FullName,
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["Budget"] = FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
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
        if (!AllowedModes.Contains(planningMode))
        {
            throw new InvalidOperationException("Invalid planning mode.");
        }

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
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void MapBrief(CampaignBrief brief, SaveCampaignBriefRequest request, DateTime now)
    {
        brief.Objective = request.Objective;
        brief.StartDate = request.StartDate;
        brief.EndDate = request.EndDate;
        brief.DurationWeeks = request.DurationWeeks;
        brief.GeographyScope = request.GeographyScope;
        brief.ProvincesJson = Serialize(request.Provinces);
        brief.CitiesJson = Serialize(request.Cities);
        brief.SuburbsJson = Serialize(request.Suburbs);
        brief.AreasJson = Serialize(request.Areas);
        brief.TargetAgeMin = request.TargetAgeMin;
        brief.TargetAgeMax = request.TargetAgeMax;
        brief.TargetGender = request.TargetGender;
        brief.TargetLanguagesJson = Serialize(request.TargetLanguages);
        brief.TargetLsmMin = request.TargetLsmMin;
        brief.TargetLsmMax = request.TargetLsmMax;
        brief.TargetInterestsJson = Serialize(request.TargetInterests);
        brief.TargetAudienceNotes = request.TargetAudienceNotes;
        brief.PreferredMediaTypesJson = Serialize(request.PreferredMediaTypes);
        brief.ExcludedMediaTypesJson = Serialize(request.ExcludedMediaTypes);
        brief.MustHaveAreasJson = Serialize(request.MustHaveAreas);
        brief.ExcludedAreasJson = Serialize(request.ExcludedAreas);
        brief.CreativeReady = request.CreativeReady;
        brief.CreativeNotes = request.CreativeNotes;
        brief.MaxMediaItems = request.MaxMediaItems;
        brief.OpenToUpsell = request.OpenToUpsell;
        brief.AdditionalBudget = request.AdditionalBudget;
        brief.SpecialRequirements = request.SpecialRequirements;
        brief.PreferredVideoAspectRatio = request.PreferredVideoAspectRatio;
        brief.PreferredVideoDurationSeconds = request.PreferredVideoDurationSeconds;
        brief.UpdatedAt = now;
    }

    private static string? Serialize<T>(T value)
    {
        return value == null ? null : JsonSerializer.Serialize(value);
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }
}

using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Advertified.App.Controllers;

/// <summary>
/// Handles campaign brief operations: initialization, submission, AI interpretation, and recommendation generation.
/// This controller orchestrates the planning phase for campaigns.
/// </summary>
[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentCampaignBriefController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICampaignBriefService _campaignBriefService;
    private readonly ICampaignRecommendationService _campaignRecommendationService;
    private readonly ICampaignBriefInterpretationService _campaignBriefInterpretationService;
    private readonly ITemplatedEmailService _emailService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AgentCampaignBriefController> _logger;

    public AgentCampaignBriefController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ICampaignBriefService campaignBriefService,
        ICampaignRecommendationService campaignRecommendationService,
        ICampaignBriefInterpretationService campaignBriefInterpretationService,
        ITemplatedEmailService emailService,
        IChangeAuditService changeAuditService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AgentCampaignBriefController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _campaignBriefService = campaignBriefService;
        _campaignRecommendationService = campaignRecommendationService;
        _campaignBriefInterpretationService = campaignBriefInterpretationService;
        _emailService = emailService;
        _changeAuditService = changeAuditService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpPost("{id:guid}/initialize-recommendation")]
    public async Task<IActionResult> InitializeRecommendation(Guid id, [FromBody] InitializeRecommendationFlowRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.CampaignBrief)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var isOrderOperationallyActive = CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder);
        if (isOrderOperationallyActive)
        {
            await _campaignBriefService.SaveDraftAsync(campaign.UserId, id, request.Brief, cancellationToken);
            if (request.SubmitBrief)
            {
                await _campaignBriefService.SubmitAsync(campaign.UserId, id, cancellationToken);
                await _campaignBriefService.SetPlanningModeAsync(campaign.UserId, id, request.PlanningMode, cancellationToken);
            }
        }
        else
        {
            var now = DateTime.UtcNow;
            var brief = campaign.CampaignBrief;
            if (brief is null)
            {
                brief = new CampaignBrief
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    CreatedAt = now
                };
                _db.CampaignBriefs.Add(brief);
            }

            MapBrief(brief, request.Brief, now);

            if (!string.IsNullOrWhiteSpace(request.CampaignName))
            {
                campaign.CampaignName = request.CampaignName.Trim();
            }

            campaign.PlanningMode = request.PlanningMode;
            campaign.AgentAssistanceRequested = request.PlanningMode is "agent_assisted" or "hybrid";
            campaign.AiUnlocked = request.SubmitBrief;
            if (request.SubmitBrief)
            {
                campaign.Status = CampaignStatuses.PlanningInProgress;
            }
            else if (string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase))
            {
                campaign.Status = CampaignStatuses.BriefInProgress;
            }
            campaign.UpdatedAt = now;

            if (request.SubmitBrief)
            {
                brief.SubmittedAt ??= now;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var campaignName = string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand.Name} campaign"
            : campaign.CampaignName.Trim();

        await WriteChangeAuditAsync(
            request.SubmitBrief ? "submit_brief" : "save_brief_draft",
            "campaign",
            campaign.Id.ToString(),
            campaignName,
            request.SubmitBrief
                ? $"Submitted campaign brief for {campaignName}."
                : $"Saved campaign brief draft for {campaignName}.",
            new
            {
                CampaignId = campaign.Id,
                request.SubmitBrief,
                request.PlanningMode
            },
            cancellationToken);

        return Ok(new { CampaignId = campaign.Id });
    }

    [HttpPost("{id:guid}/generate-recommendation")]
    public async Task<IActionResult> GenerateRecommendation(Guid id, [FromBody] GenerateRecommendationRequest? request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        campaign.AssignedAgentUserId ??= currentUserId;
        campaign.AssignedAt ??= DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await SendAssignmentEmailIfNeededAsync(campaign.Id, cancellationToken);
        await SendAgentWorkStartedEmailIfNeededAsync(campaign.Id, cancellationToken);

        try
        {
            await _campaignRecommendationService.GenerateAndSaveAsync(id, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }

        var latestRecommendation = await _db.CampaignRecommendations
            .AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderByDescending(x => x.RevisionNumber)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var campaignName = string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand.Name} campaign"
            : campaign.CampaignName;

        await WriteChangeAuditAsync(
            "generate_recommendation",
            "campaign",
            campaign.Id.ToString(),
            campaignName,
            $"Generated recommendation set for {campaignName}.",
            new
            {
                CampaignId = campaign.Id,
                RecommendationId = latestRecommendation?.Id,
                RevisionNumber = latestRecommendation?.RevisionNumber,
                Status = latestRecommendation?.Status
            },
            cancellationToken);

        return Ok(new { CampaignId = campaign.Id });
    }

    [HttpPost("{id:guid}/interpret-brief")]
    public async Task<IActionResult> InterpretBrief(Guid id, [FromBody] InterpretCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Brief))
        {
            throw new InvalidOperationException("Campaign brief is required.");
        }

        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        request.SelectedBudget = request.SelectedBudget > 0
            ? request.SelectedBudget
            : PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount);

        var interpretation = await _campaignBriefInterpretationService.InterpretAsync(request, cancellationToken);
        return Ok(interpretation);
    }

    private static void MapBrief(CampaignBrief brief, SaveCampaignBriefRequest request, DateTime now)
    {
        var normalizedScope = NormalizeGeographyScope(request.GeographyScope);
        var normalizedProvinces = NormalizeScopeList(request.Provinces, normalizedScope == "provincial");
        var normalizedCities = NormalizeScopeList(request.Cities, normalizedScope == "local");
        var normalizedSuburbs = NormalizeScopeList(request.Suburbs, normalizedScope == "local");
        var normalizedAreas = NormalizeScopeList(request.Areas, normalizedScope == "local");

        brief.Objective = request.Objective;
        brief.BusinessStage = request.BusinessStage;
        brief.MonthlyRevenueBand = request.MonthlyRevenueBand;
        brief.SalesModel = request.SalesModel;
        brief.StartDate = request.StartDate;
        brief.EndDate = request.EndDate;
        brief.DurationWeeks = request.DurationWeeks;
        brief.GeographyScope = normalizedScope;
        brief.ProvincesJson = Serialize(normalizedProvinces);
        brief.CitiesJson = Serialize(normalizedCities);
        brief.SuburbsJson = Serialize(normalizedSuburbs);
        brief.AreasJson = Serialize(normalizedAreas);
        brief.TargetAgeMin = request.TargetAgeMin;
        brief.TargetAgeMax = request.TargetAgeMax;
        brief.TargetGender = request.TargetGender;
        brief.TargetLanguagesJson = Serialize(request.TargetLanguages);
        brief.TargetLsmMin = request.TargetLsmMin;
        brief.TargetLsmMax = request.TargetLsmMax;
        brief.TargetInterestsJson = Serialize(request.TargetInterests);
        brief.TargetAudienceNotes = request.TargetAudienceNotes;
        brief.CustomerType = request.CustomerType;
        brief.BuyingBehaviour = request.BuyingBehaviour;
        brief.DecisionCycle = request.DecisionCycle;
        brief.PricePositioning = request.PricePositioning;
        brief.AverageCustomerSpendBand = request.AverageCustomerSpendBand;
        brief.GrowthTarget = request.GrowthTarget;
        brief.UrgencyLevel = request.UrgencyLevel;
        brief.AudienceClarity = request.AudienceClarity;
        brief.ValuePropositionFocus = request.ValuePropositionFocus;
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

    private static string NormalizeGeographyScope(string? scope)
    {
        return (scope ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "regional" => "provincial",
            "local" => "local",
            "provincial" => "provincial",
            "national" => "national",
            _ => "provincial"
        };
    }

    private static IReadOnlyList<string>? NormalizeScopeList(IReadOnlyList<string>? values, bool enabled)
    {
        if (!enabled || values is null)
        {
            return null;
        }

        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
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

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private async Task SendAssignmentEmailIfNeededAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.AssignmentEmailSentAt.HasValue || campaign.AssignedAgentUserId is null || IsProspectiveCampaign(campaign))
        {
            return;
        }

        try
        {
            await _emailService.SendAsync(
                "campaign-assigned",
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
            _logger.LogError(ex, "Failed to send assignment email for campaign {CampaignId}.", campaign.Id);
            return;
        }

        campaign.AssignmentEmailSentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendAgentWorkStartedEmailIfNeededAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.AgentWorkStartedEmailSentAt.HasValue || IsProspectiveCampaign(campaign))
        {
            return;
        }

        try
        {
            await _emailService.SendAsync(
                "agent-working",
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
            _logger.LogError(ex, "Failed to send agent working email for campaign {CampaignId}.", campaign.Id);
            return;
        }

        campaign.AgentWorkStartedEmailSentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-ZA"));
    }

    private static bool IsProspectiveCampaign(Campaign campaign)
    {
        return string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase)
            || (campaign.PackageOrder?.PaymentProvider == "prospect" && 
                !string.Equals(campaign.PackageOrder?.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase));
    }
}

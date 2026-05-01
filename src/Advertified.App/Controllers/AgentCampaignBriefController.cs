using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
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
    private readonly ILeadProposalConfidenceGateService _leadProposalConfidenceGateService;
    private readonly ICampaignBriefInterpretationService _campaignBriefInterpretationService;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly ITemplatedEmailService _emailService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AgentCampaignBriefController> _logger;

    public AgentCampaignBriefController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ICampaignBriefService campaignBriefService,
        ICampaignRecommendationService campaignRecommendationService,
        ILeadProposalConfidenceGateService leadProposalConfidenceGateService,
        ICampaignBriefInterpretationService campaignBriefInterpretationService,
        IAgentCampaignOwnershipService ownershipService,
        ITemplatedEmailService emailService,
        IChangeAuditService changeAuditService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AgentCampaignBriefController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _campaignBriefService = campaignBriefService;
        _campaignRecommendationService = campaignRecommendationService;
        _leadProposalConfidenceGateService = leadProposalConfidenceGateService;
        _campaignBriefInterpretationService = campaignBriefInterpretationService;
        _ownershipService = ownershipService;
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
        var campaign = await _ownershipService.GetOwnedOrClaimableCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.PackageOrder)
                .Include(x => x.PackageBand)
                .Include(x => x.CampaignBrief)
                .Include(x => x.ProspectLead),
            cancellationToken);
        _ownershipService.TryClaim(campaign, currentUser, DateTime.UtcNow);

        var isOrderOperationallyActive = CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder);
        if (isOrderOperationallyActive && campaign.UserId.HasValue)
        {
            await _campaignBriefService.SaveDraftAsync(campaign.UserId.Value, id, request.Brief, cancellationToken);
            if (request.SubmitBrief)
            {
                await _campaignBriefService.SubmitAsync(campaign.UserId.Value, id, cancellationToken);
                await _campaignBriefService.SetPlanningModeAsync(campaign.UserId.Value, id, request.PlanningMode, cancellationToken);
            }
        }
        else
        {
            await _campaignBriefService.SaveAgentManagedAsync(
                campaign,
                request.Brief,
                request.PlanningMode,
                request.CampaignName,
                request.SubmitBrief,
                cancellationToken);
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
        var campaign = await _ownershipService.GetOwnedOrClaimableCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.User)
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder),
            cancellationToken);

        var now = DateTime.UtcNow;
        _ownershipService.TryClaim(campaign, currentUser, now);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await SendAssignmentEmailIfNeededAsync(campaign.Id, cancellationToken);
        await SendAgentWorkStartedEmailIfNeededAsync(campaign.Id, cancellationToken);

        try
        {
            await _leadProposalConfidenceGateService.EnsureCampaignReadyAsync(id, cancellationToken);
            await _campaignRecommendationService.GenerateAndSaveAsync(id, request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
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
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Brief))
        {
            throw new InvalidOperationException("Campaign brief is required.");
        }

        var campaign = await _ownershipService.GetOwnedOrClaimableCampaignAsync(
            id,
            currentUser,
            query => query.Include(x => x.PackageOrder),
            cancellationToken);

        if (_ownershipService.TryClaim(campaign, currentUser, DateTime.UtcNow))
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        request.SelectedBudget = request.SelectedBudget > 0
            ? request.SelectedBudget
            : PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount);

        var interpretation = await _campaignBriefInterpretationService.InterpretAsync(request, cancellationToken);
        return Ok(interpretation);
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

    private string BuildClientCampaignUrl(Campaign campaign)
    {
        return campaign.UserId.HasValue
            ? BuildFrontendUrl($"/campaigns/{campaign.Id}")
            : BuildFrontendUrl($"/register?next=%2Fcampaigns%2F{campaign.Id:D}");
    }

    private async Task SendAssignmentEmailIfNeededAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.EmailDeliveryMessages)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null
            || campaign.AssignedAgentUserId is null
            || ProspectCampaignPolicy.IsProspectiveCampaign(campaign)
            || EmailDeliveryPurposePolicy.HasTrackedDelivery(campaign.EmailDeliveryMessages, "campaign_assigned"))
        {
            return;
        }

        try
        {
            await _emailService.SendAsync(
                "campaign-assigned",
                campaign.ResolveClientEmail(),
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.ResolveClientName(),
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["Budget"] = FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                    ["CampaignUrl"] = BuildClientCampaignUrl(campaign)
                },
                null,
                new EmailTrackingContext
                {
                    Purpose = "campaign_assigned",
                    CampaignId = campaign.Id,
                    RecipientUserId = campaign.UserId,
                    ProspectLeadId = campaign.ProspectLeadId
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send assignment email for campaign {CampaignId}.", campaign.Id);
            return;
        }
    }

    private async Task SendAgentWorkStartedEmailIfNeededAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.EmailDeliveryMessages)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null
            || ProspectCampaignPolicy.IsProspectiveCampaign(campaign)
            || EmailDeliveryPurposePolicy.HasTrackedDelivery(campaign.EmailDeliveryMessages, "agent_work_started"))
        {
            return;
        }

        try
        {
            await _emailService.SendAsync(
                "agent-working",
                campaign.ResolveClientEmail(),
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.ResolveClientName(),
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["Budget"] = FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                    ["CampaignUrl"] = BuildClientCampaignUrl(campaign)
                },
                null,
                new EmailTrackingContext
                {
                    Purpose = "agent_work_started",
                    CampaignId = campaign.Id,
                    RecipientUserId = campaign.UserId,
                    ProspectLeadId = campaign.ProspectLeadId
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send agent working email for campaign {CampaignId}.", campaign.Id);
            return;
        }
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-ZA"));
    }

}

using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Advertified.App.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Advertified.App.Controllers;

[ApiController]
[Route("campaigns")]
[Authorize]
public sealed class CampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICampaignAccessService _campaignAccessService;
    private readonly ICampaignBriefService _campaignBriefService;
    private readonly ICampaignRecommendationService _campaignRecommendationService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly IRecommendationApprovalWorkflowService _recommendationApprovalWorkflowService;
    private readonly ICampaignPlanningTargetResolver _planningTargetResolver;
    private readonly ICampaignBusinessLocationResolver _businessLocationResolver;
    private readonly ICampaignExecutionTaskService _campaignExecutionTaskService;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly CampaignPlanningRequestValidator _campaignPlanningRequestValidator;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<CampaignsController> _logger;

    public CampaignsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ICampaignAccessService campaignAccessService,
        ICampaignBriefService campaignBriefService,
        ICampaignRecommendationService campaignRecommendationService,
        IRecommendationDocumentService recommendationDocumentService,
        IRecommendationApprovalWorkflowService recommendationApprovalWorkflowService,
        ICampaignPlanningTargetResolver planningTargetResolver,
        ICampaignBusinessLocationResolver businessLocationResolver,
        ICampaignExecutionTaskService campaignExecutionTaskService,
        IPackagePurchaseService packagePurchaseService,
        CampaignPlanningRequestValidator campaignPlanningRequestValidator,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<CampaignsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _campaignAccessService = campaignAccessService;
        _campaignBriefService = campaignBriefService;
        _campaignRecommendationService = campaignRecommendationService;
        _recommendationDocumentService = recommendationDocumentService;
        _recommendationApprovalWorkflowService = recommendationApprovalWorkflowService;
        _planningTargetResolver = planningTargetResolver;
        _businessLocationResolver = businessLocationResolver;
        _campaignExecutionTaskService = campaignExecutionTaskService;
        _packagePurchaseService = packagePurchaseService;
        _campaignPlanningRequestValidator = campaignPlanningRequestValidator;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CampaignListItemResponse>>> Get(CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return Ok(campaigns.Select(x => x.ToListItem()).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignCreativeSystems)
            .Include(x => x.CampaignAssets)
            .Include(x => x.CampaignExecutionTasks)
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationRunAudits)
            .Include(x => x.EmailDeliveryMessages)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (campaign is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var response = campaign.ToDetail(includeLinePricing: false);
        var resolvedBusinessLocation = _businessLocationResolver.Resolve(campaign);
        if (!string.IsNullOrWhiteSpace(resolvedBusinessLocation.Label))
        {
            response.BusinessLocation = new CampaignPlanningTargetResponse
            {
                Label = resolvedBusinessLocation.Label,
                Area = resolvedBusinessLocation.Area,
                City = resolvedBusinessLocation.City,
                Province = resolvedBusinessLocation.Province,
                Latitude = resolvedBusinessLocation.Latitude,
                Longitude = resolvedBusinessLocation.Longitude,
                Source = resolvedBusinessLocation.Source,
                Precision = resolvedBusinessLocation.Precision
            };
        }

        var resolvedTarget = _planningTargetResolver.Resolve(campaign.CampaignBrief);
        if (!string.IsNullOrWhiteSpace(resolvedTarget.Label))
        {
            response.EffectivePlanningTarget = new CampaignPlanningTargetResponse
            {
                Scope = campaign.CampaignBrief?.GeographyScope,
                Label = resolvedTarget.Label,
                City = resolvedTarget.City,
                Province = resolvedTarget.Province,
                Latitude = resolvedTarget.Latitude,
                Longitude = resolvedTarget.Longitude,
                Source = resolvedTarget.Source,
                Precision = resolvedTarget.Precision,
                PriorityAreas = campaign.CampaignBrief is null
                    ? Array.Empty<string>()
                    : Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(campaign.CampaignBrief, nameof(CampaignBrief.MustHaveAreasJson)),
                Exclusions = campaign.CampaignBrief is null
                    ? Array.Empty<string>()
                    : Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(campaign.CampaignBrief, nameof(CampaignBrief.ExcludedAreasJson))
            };
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/performance")]
    public async Task<ActionResult<CampaignPerformanceSnapshotResponse>> GetPerformance(Guid id, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.CampaignChannelMetrics)
            .Include(x => x.CampaignSupplierBookings)
            .Include(x => x.CampaignDeliveryReports)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

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

    [HttpGet("{id:guid}/access")]
    public async Task<ActionResult<CampaignAccessResponse>> GetAccess(Guid id, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (campaign is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var workflow = CampaignWorkflowPolicy.BuildClientWorkflow(campaign);

        return Ok(new CampaignAccessResponse(workflow.CanOpenBrief, workflow.CanOpenPlanning));
    }

    [HttpGet("{id:guid}/recommendation-pdf")]
    public async Task<IActionResult> DownloadRecommendationPdf(Guid id, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (!campaignExists)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        try
        {
            var bytes = await _recommendationDocumentService.GetCampaignPdfBytesAsync(id, cancellationToken);
            return File(bytes, "application/pdf", $"recommendation-{id:D}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Lead proposal confidence gate failed.",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet("recommendation-pdf-sample")]
    public IActionResult DownloadRecommendationPdfSample()
    {
        var bytes = RecommendationPdfPreviewFactory.GenerateSample();
        return File(bytes, "application/pdf", "recommendation-sample.pdf");
    }

    [HttpPut("{id:guid}/brief")]
    public async Task<IActionResult> SaveBrief(Guid id, [FromBody] SaveCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _campaignAccessService.EnsureCanEditBriefAsync(userId, id, cancellationToken);
        await _campaignBriefService.SaveDraftAsync(userId, id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/brief/submit")]
    public async Task<IActionResult> SubmitBrief(Guid id, [FromBody] SubmitCampaignBriefRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _campaignBriefService.SubmitAsync(userId, id, cancellationToken);
        return Accepted(new { CampaignId = id, Message = "Campaign brief submitted." });
    }

    [HttpPost("{id:guid}/planning-mode")]
    public async Task<IActionResult> SetPlanningMode(Guid id, [FromBody] SetPlanningModeRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _campaignBriefService.SetPlanningModeAsync(userId, id, request.PlanningMode, cancellationToken);
        return Accepted(new { CampaignId = id, request.PlanningMode });
    }

    [HttpPost("{id:guid}/generate-plan")]
    public async Task<IActionResult> GeneratePlan(Guid id, [FromBody] CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        await _campaignPlanningRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.CampaignId != Guid.Empty && request.CampaignId != id)
        {
            return BadRequest(new { Message = "Campaign ID in the request body must match the route ID." });
        }

        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _campaignAccessService.EnsureCanGeneratePlanAsync(userId, id, cancellationToken);
        var recommendationId = await _campaignRecommendationService.GenerateAndSaveAsync(id, null, cancellationToken);
        return Accepted(new
        {
            CampaignId = id,
            RecommendationId = recommendationId,
            Status = CampaignStatuses.PlanningInProgress
        });
    }

    [HttpPost("{id:guid}/approve-recommendation")]
    public async Task<IActionResult> ApproveRecommendation(Guid id, [FromBody] ApproveRecommendationRequest? request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (!campaignExists)
        {
            throw new NotFoundException("Campaign not found.");
        }

        var result = await _recommendationApprovalWorkflowService.ApproveAsync(id, request?.RecommendationId, cancellationToken);

        return Accepted(new
        {
            result.CampaignId,
            result.RecommendationId,
            result.Status,
            result.Message
        });
    }

    [HttpPost("{id:guid}/prepare-checkout")]
    public async Task<IActionResult> PrepareCheckout(Guid id, [FromBody] ApproveRecommendationRequest? request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (!campaignExists)
        {
            throw new NotFoundException("Campaign not found.");
        }

        if (!request?.RecommendationId.HasValue ?? true)
        {
            throw new BadRequestException("Recommendation not found.");
        }

        await _packagePurchaseService.PrepareRecommendationCheckoutAsync(id, request!.RecommendationId!.Value, cancellationToken);
        return Accepted(new { CampaignId = id, RecommendationId = request.RecommendationId.Value, Message = "Checkout prepared." });
    }

    [HttpPost("{id:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(Guid id, [FromBody] RequestRecommendationChangesRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (!campaignExists)
        {
            throw new NotFoundException("Campaign not found.");
        }

        var result = await _recommendationApprovalWorkflowService.RequestChangesAsync(id, request.Notes, cancellationToken);

        return Accepted(new
        {
            result.CampaignId,
            result.RecommendationId,
            result.Status,
            result.Message
        });
    }

    [HttpPost("{id:guid}/reject-all-recommendations")]
    public async Task<IActionResult> RejectAllRecommendations(Guid id, [FromBody] RequestRecommendationChangesRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (!campaignExists)
        {
            throw new NotFoundException("Campaign not found.");
        }

        var result = await _recommendationApprovalWorkflowService.RejectAllAsync(id, request.Notes, cancellationToken);

        return Accepted(new
        {
            result.CampaignId,
            result.RecommendationId,
            result.Status,
            result.Message
        });
    }

    [HttpPost("{id:guid}/approve-creative")]
    public async Task<IActionResult> ApproveCreative(Guid id, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.AssignedAgentUser)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeSentToClientForApproval, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Creative is not ready for approval.",
                Detail = "Finished media can only be approved once it has been sent back to the client for final review.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        CampaignStatusTransitionPolicy.TryAdvanceToBookingInProgress(campaign);
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _campaignExecutionTaskService.MarkTaskCompletedAsync(campaign.Id, "creative_handoff", cancellationToken);
        await _campaignExecutionTaskService.MarkTaskOpenAsync(campaign.Id, "booking_confirmation", cancellationToken);
        await SendInternalCreativeQueueUpdateAsync(
            campaign,
            eventTitle: "Client approved final creative",
            eventBody: "Final creative approval captured. Supplier booking and launch preparation can now begin.",
            actionPath: $"/creative/campaigns/{campaign.Id}/studio",
            includeAssignedAgent: true,
            cancellationToken);

        return Accepted(new
        {
            CampaignId = id,
            Status = campaign.Status,
            Message = "Creative approved."
        });
    }

    [HttpPost("{id:guid}/request-creative-changes")]
    public async Task<IActionResult> RequestCreativeChanges(Guid id, [FromBody] RequestRecommendationChangesRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.CampaignConversation!)
                .ThenInclude(x => x.Messages)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.AssignedAgentUser)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeSentToClientForApproval, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Creative is not ready for revision.",
                Detail = "Creative changes can only be requested after finished media has been sent back for client approval.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var now = DateTime.UtcNow;
        campaign.Status = CampaignStatuses.CreativeChangesRequested;
        campaign.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            var conversation = campaign.CampaignConversation;
            if (conversation is null)
            {
                conversation = new Data.Entities.CampaignConversation
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    ClientUserId = campaign.UserId ?? throw new InvalidOperationException("Campaign is not linked to a client account yet."),
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastMessageAt = now
                };
                campaign.CampaignConversation = conversation;
                _db.CampaignConversations.Add(conversation);
            }

            conversation.UpdatedAt = now;
            conversation.LastMessageAt = now;
            conversation.Messages.Add(new Data.Entities.CampaignMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderUserId = userId,
                SenderRole = "client",
                Body = request.Notes.Trim(),
                CreatedAt = now,
                ReadByClientAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await SendInternalCreativeQueueUpdateAsync(
            campaign,
            eventTitle: "Creative revisions requested by client",
            eventBody: "Client requested creative changes. Review notes and issue a revised handoff.",
            actionPath: $"/creative/campaigns/{campaign.Id}/studio",
            includeAssignedAgent: true,
            cancellationToken);

        return Accepted(new
        {
            CampaignId = id,
            Status = campaign.Status,
            Message = "Creative changes requested."
        });
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }

    private async Task SendInternalCreativeQueueUpdateAsync(
        Campaign campaign,
        string eventTitle,
        string eventBody,
        string actionPath,
        bool includeAssignedAgent,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipientEmails = await GetInternalCreativeNotificationRecipientsAsync(campaign, includeAssignedAgent, cancellationToken);
            if (recipientEmails.Length == 0)
            {
                return;
            }

            var campaignName = string.IsNullOrWhiteSpace(campaign.CampaignName)
                ? $"{campaign.PackageBand.Name} campaign"
                : campaign.CampaignName.Trim();
            var actionUrl = BuildFrontendUrl(actionPath);

            foreach (var email in recipientEmails)
            {
                await _emailService.SendAsync(
                    "creative-queue-update",
                    email,
                    "campaigns",
                    new Dictionary<string, string?>
                    {
                        ["CampaignName"] = campaignName,
                        ["PackageName"] = campaign.PackageBand.Name,
                        ["Budget"] = FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                        ["EventTitle"] = eventTitle,
                        ["EventBody"] = eventBody,
                        ["ActionUrl"] = actionUrl
                    },
                    null,
                    null,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send internal creative queue update email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task<string[]> GetInternalCreativeNotificationRecipientsAsync(
        Campaign campaign,
        bool includeAssignedAgent,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var creativeDirectorEmails = await _db.UserAccounts
            .AsNoTracking()
            .Where(x => x.Role == UserRole.CreativeDirector && x.AccountStatus == AccountStatus.Active)
            .Select(x => x.Email)
            .ToArrayAsync(cancellationToken);

        foreach (var email in creativeDirectorEmails)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                recipients.Add(email.Trim());
            }
        }

        if (includeAssignedAgent && !string.IsNullOrWhiteSpace(campaign.AssignedAgentUser?.Email))
        {
            recipients.Add(campaign.AssignedAgentUser.Email.Trim());
        }

        return recipients.ToArray();
    }
}

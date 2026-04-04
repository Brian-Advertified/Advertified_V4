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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Advertified.App.Controllers;

[ApiController]
[Route("campaigns")]
public sealed class CampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICampaignAccessService _campaignAccessService;
    private readonly ICampaignBriefService _campaignBriefService;
    private readonly ICampaignRecommendationService _campaignRecommendationService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly IRecommendationApprovalWorkflowService _recommendationApprovalWorkflowService;
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
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignCreativeSystems)
            .Include(x => x.CampaignAssets)
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (campaign is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(campaign.ToDetail(includeLinePricing: false));
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

        var bytes = await _recommendationDocumentService.GetCampaignPdfBytesAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"recommendation-{id:D}.pdf");
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
            throw new InvalidOperationException("Campaign not found.");
        }

        RecommendationWorkflowResult result;
        try
        {
            result = await _recommendationApprovalWorkflowService.ApproveAsync(id, request?.RecommendationId, cancellationToken);
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Payment required before approval.", StringComparison.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Payment required before approval.",
                Detail = "Please complete payment for this campaign before approving a recommendation.",
                Status = StatusCodes.Status400BadRequest
            });
        }

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
            throw new InvalidOperationException("Campaign not found.");
        }

        if (!request?.RecommendationId.HasValue ?? true)
        {
            throw new InvalidOperationException("Recommendation not found.");
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
            throw new InvalidOperationException("Campaign not found.");
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
                    ClientUserId = campaign.UserId,
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

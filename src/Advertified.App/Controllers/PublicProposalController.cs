using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/proposals")]
[AllowAnonymous]
[EnableRateLimiting("public_proposal")]
public sealed class PublicProposalController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProposalAccessTokenService _proposalAccessTokenService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly ILeadProposalConfidenceGateService _leadProposalConfidenceGateService;
    private readonly IRecommendationApprovalWorkflowService _recommendationApprovalWorkflowService;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly IChangeAuditService _changeAuditService;

    public PublicProposalController(
        AppDbContext db,
        IProposalAccessTokenService proposalAccessTokenService,
        IRecommendationDocumentService recommendationDocumentService,
        ILeadProposalConfidenceGateService leadProposalConfidenceGateService,
        IRecommendationApprovalWorkflowService recommendationApprovalWorkflowService,
        IPackagePurchaseService packagePurchaseService,
        IChangeAuditService changeAuditService)
    {
        _db = db;
        _proposalAccessTokenService = proposalAccessTokenService;
        _recommendationDocumentService = recommendationDocumentService;
        _leadProposalConfidenceGateService = leadProposalConfidenceGateService;
        _recommendationApprovalWorkflowService = recommendationApprovalWorkflowService;
        _packagePurchaseService = packagePurchaseService;
        _changeAuditService = changeAuditService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDetailResponse>> GetById(Guid id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        await EnsureValidTokenAsync(id, token, cancellationToken);

        var campaign = await LoadCampaignAsync(id, cancellationToken)
            ?? throw new NotFoundException("Campaign not found.");

        return Ok(campaign.ToDetail(includeLinePricing: false));
    }

    [HttpGet("{id:guid}/recommendation-pdf")]
    public async Task<IActionResult> DownloadRecommendationPdf(Guid id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        await EnsureValidTokenAsync(id, token, cancellationToken);

        var campaignExists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

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
            await _leadProposalConfidenceGateService.EnsureCampaignReadyAsync(id, cancellationToken);
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

        var bytes = await _recommendationDocumentService.GetCampaignPdfBytesAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"recommendation-{id:D}.pdf");
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] PublicProposalActionRequest request, CancellationToken cancellationToken)
    {
        await EnsureValidTokenAsync(id, request.Token, cancellationToken);
        var result = await _recommendationApprovalWorkflowService.ApproveAsync(id, request.RecommendationId, cancellationToken);

        return Accepted(new
        {
            result.CampaignId,
            result.RecommendationId,
            result.Status,
            result.Message
        });
    }

    [HttpPost("{id:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(Guid id, [FromBody] PublicProposalActionRequest request, CancellationToken cancellationToken)
    {
        await EnsureValidTokenAsync(id, request.Token, cancellationToken);
        var result = await _recommendationApprovalWorkflowService.RequestChangesAsync(id, request.Notes, cancellationToken);

        return Accepted(new
        {
            result.CampaignId,
            result.RecommendationId,
            result.Status,
            result.Message
        });
    }

    [HttpPost("{id:guid}/prepare-checkout")]
    public async Task<IActionResult> PrepareCheckout(Guid id, [FromBody] PublicProposalActionRequest request, CancellationToken cancellationToken)
    {
        await EnsureValidTokenAsync(id, request.Token, cancellationToken);
        if (!request.RecommendationId.HasValue)
        {
            throw new BadRequestException("Recommendation not found.");
        }

        await _packagePurchaseService.PrepareRecommendationCheckoutAsync(id, request.RecommendationId.Value, cancellationToken);
        return Accepted(new { CampaignId = id, RecommendationId = request.RecommendationId.Value, Message = "Checkout prepared." });
    }

    [HttpPost("{id:guid}/lead-engagement")]
    public async Task<IActionResult> TrackLeadEngagement(Guid id, [FromBody] PublicLeadEngagementRequest request, CancellationToken cancellationToken)
    {
        await EnsureValidTokenAsync(id, request.Token, cancellationToken);

        var eventType = (request.EventType ?? string.Empty).Trim().ToLowerInvariant();
        if (eventType is not ("page_view" or "reply_click" or "download_pdf_click" or "callback_click"))
        {
            throw new BadRequestException("Unsupported lead engagement event type.");
        }

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.CampaignBrief)
            .Include(x => x.ProspectLead)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Campaign not found.");

        if (!LeadOutreachCampaignSupport.IsLeadOutreachCampaign(campaign))
        {
            return Accepted(new { CampaignId = id, EventType = eventType, Message = "Ignored for non-lead campaign." });
        }

        await _changeAuditService.WriteAsync(
            actorUserId: null,
            scope: "public",
            action: $"lead_proposal_{eventType}",
            entityType: "campaign",
            entityId: campaign.Id.ToString(),
            entityLabel: campaign.CampaignName,
            summary: $"Public lead proposal engagement: {eventType}.",
            metadata: new
            {
                CampaignId = campaign.Id,
                EventType = eventType,
                request.Context,
                request.PageUrl,
                request.UserAgent,
                ProspectEmail = campaign.ProspectLead?.Email
            },
            cancellationToken);

        return Accepted(new { CampaignId = id, EventType = eventType, Message = "Engagement tracked." });
    }

    private async Task EnsureValidTokenAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        if (!_proposalAccessTokenService.TryReadToken(token, out var payload) || payload.CampaignId != campaignId)
        {
            throw new BadRequestException("This secure proposal link is invalid or has expired.");
        }

        var exists = await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(x => x.Id == campaignId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Campaign not found.");
        }
    }

    private Task<Advertified.App.Data.Entities.Campaign?> LoadCampaignAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.ProspectLead)
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public sealed class PublicLeadEngagementRequest
    {
        public string Token { get; init; } = string.Empty;

        public string EventType { get; init; } = string.Empty;

        public string? Context { get; init; }

        public string? PageUrl { get; init; }

        public string? UserAgent { get; init; }
    }
}

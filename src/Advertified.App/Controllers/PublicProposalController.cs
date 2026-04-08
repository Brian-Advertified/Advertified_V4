using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/proposals")]
[AllowAnonymous]
public sealed class PublicProposalController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProposalAccessTokenService _proposalAccessTokenService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly IRecommendationApprovalWorkflowService _recommendationApprovalWorkflowService;
    private readonly IPackagePurchaseService _packagePurchaseService;

    public PublicProposalController(
        AppDbContext db,
        IProposalAccessTokenService proposalAccessTokenService,
        IRecommendationDocumentService recommendationDocumentService,
        IRecommendationApprovalWorkflowService recommendationApprovalWorkflowService,
        IPackagePurchaseService packagePurchaseService)
    {
        _db = db;
        _proposalAccessTokenService = proposalAccessTokenService;
        _recommendationDocumentService = recommendationDocumentService;
        _recommendationApprovalWorkflowService = recommendationApprovalWorkflowService;
        _packagePurchaseService = packagePurchaseService;
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
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

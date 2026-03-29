using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
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

        return Ok(campaign.ToDetail());
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
            Status = "planning_in_progress"
        });
    }

    [HttpPost("{id:guid}/approve-recommendation")]
    public async Task<IActionResult> ApproveRecommendation(Guid id, [FromBody] ApproveRecommendationRequest? request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.CampaignRecommendations)
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        var recommendation = currentRecommendations
            .FirstOrDefault(x => x.Id == request?.RecommendationId)
            ?? currentRecommendations.FirstOrDefault()
            ?? throw new InvalidOperationException("Recommendation not found.");

        recommendation.Status = "approved";
        recommendation.ApprovedAt = DateTime.UtcNow;
        recommendation.UpdatedAt = DateTime.UtcNow;
        campaign.Status = "approved";
        campaign.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await SendRecommendationApprovedEmailAsync(campaign, cancellationToken);
        await SendActivationInProgressEmailAsync(campaign, cancellationToken);

        return Accepted(new
        {
            CampaignId = id,
            RecommendationId = recommendation.Id,
            Status = recommendation.Status,
            Message = "Recommendation approved."
        });
    }

    [HttpPost("{id:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(Guid id, [FromBody] RequestRecommendationChangesRequest request, CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }

        var now = DateTime.UtcNow;
        var nextRevisionNumber = RecommendationRevisionSupport.GetNextRevisionNumber(campaign.CampaignRecommendations);
        var clonedRecommendations = RecommendationRevisionSupport.CloneAsDraftRevision(currentRecommendations, nextRevisionNumber, now, request.Notes);
        _db.CampaignRecommendations.AddRange(clonedRecommendations);
        campaign.Status = "planning_in_progress";
        campaign.RecommendationReadyEmailSentAt = null;
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return Accepted(new
        {
            CampaignId = id,
            RecommendationId = clonedRecommendations[0].Id,
            Status = campaign.Status,
            Message = "Recommendation returned for changes."
        });
    }

    private async Task SendRecommendationApprovedEmailAsync(Advertified.App.Data.Entities.Campaign campaign, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "recommendation-approved",
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
            _logger.LogError(ex, "Failed to send recommendation approved email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendActivationInProgressEmailAsync(Advertified.App.Data.Entities.Campaign campaign, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "activation-in-progress",
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
            _logger.LogError(ex, "Failed to send activation in progress email for campaign {CampaignId}.", campaign.Id);
        }
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

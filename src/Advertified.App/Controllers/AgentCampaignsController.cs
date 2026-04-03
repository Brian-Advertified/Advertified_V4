using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/campaigns")]
public sealed class AgentCampaignsController : ControllerBase
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private const string ManualReviewMarker = "Manual review required:";
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICampaignBriefService _campaignBriefService;
    private readonly ICampaignRecommendationService _campaignRecommendationService;
    private readonly ICampaignBriefInterpretationService _campaignBriefInterpretationService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IPublicAssetStorage _assetStorage;
    private readonly ITemplatedEmailService _emailService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly FrontendOptions _frontendOptions;
    private readonly IProposalAccessTokenService _proposalAccessTokenService;
    private readonly ILogger<AgentCampaignsController> _logger;

    public AgentCampaignsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ICampaignBriefService campaignBriefService,
        ICampaignRecommendationService campaignRecommendationService,
        ICampaignBriefInterpretationService campaignBriefInterpretationService,
        IRecommendationDocumentService recommendationDocumentService,
        IPackagePurchaseService packagePurchaseService,
        IPasswordHashingService passwordHashingService,
        IEmailVerificationService emailVerificationService,
        IPublicAssetStorage assetStorage,
        ITemplatedEmailService emailService,
        IChangeAuditService changeAuditService,
        IOptions<FrontendOptions> frontendOptions,
        IProposalAccessTokenService proposalAccessTokenService,
        ILogger<AgentCampaignsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _campaignBriefService = campaignBriefService;
        _campaignRecommendationService = campaignRecommendationService;
        _campaignBriefInterpretationService = campaignBriefInterpretationService;
        _recommendationDocumentService = recommendationDocumentService;
        _packagePurchaseService = packagePurchaseService;
        _passwordHashingService = passwordHashingService;
        _emailVerificationService = emailVerificationService;
        _assetStorage = assetStorage;
        _emailService = emailService;
        _changeAuditService = changeAuditService;
        _frontendOptions = frontendOptions.Value;
        _proposalAccessTokenService = proposalAccessTokenService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AgentCampaignListProjection
            {
                Id = x.Id,
                PackageOrderId = x.PackageOrderId,
                PackageBandId = x.PackageBandId,
                PackageBandName = x.PackageBand.Name,
                SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                    x.PackageOrder.SelectedBudget ?? x.PackageOrder.Amount,
                    x.PackageOrder.AiStudioReserveAmount),
                CampaignName = x.CampaignName,
                Status = x.Status,
                PlanningMode = x.PlanningMode,
                AiUnlocked = x.AiUnlocked,
                AgentAssistanceRequested = x.AgentAssistanceRequested,
                AssignedAgentUserId = x.AssignedAgentUserId,
                AssignedAgentName = x.AssignedAgentUser != null ? x.AssignedAgentUser.FullName : null,
                AssignedAt = x.AssignedAt,
                CreatedAt = x.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        return Ok(campaigns.Select(x => new CampaignListItemResponse
        {
            Id = x.Id,
            PackageOrderId = x.PackageOrderId,
            PackageBandId = x.PackageBandId,
            PackageBandName = x.PackageBandName,
            SelectedBudget = x.SelectedBudget,
            CampaignName = x.CampaignName,
            Status = x.Status,
            PlanningMode = x.PlanningMode,
            AiUnlocked = x.AiUnlocked,
            AgentAssistanceRequested = x.AgentAssistanceRequested,
            AssignedAgentUserId = x.AssignedAgentUserId,
            AssignedAgentName = x.AssignedAgentName,
            AssignedAt = x.AssignedAt.HasValue ? new DateTimeOffset(x.AssignedAt.Value, TimeSpan.Zero) : null,
            IsAssignedToCurrentUser = x.AssignedAgentUserId == currentUserId,
            IsUnassigned = x.AssignedAgentUserId is null,
            NextAction = CampaignWorkflowPolicy.GetClientNextAction(new Campaign { Status = x.Status }),
            CreatedAt = new DateTimeOffset(x.CreatedAt, TimeSpan.Zero)
        }).ToArray());
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AgentInboxProjection
            {
                Id = x.Id,
                UserId = x.UserId,
                CampaignName = x.CampaignName,
                PackageBandId = x.PackageBandId,
                PackageBandName = x.PackageBand.Name,
                ClientName = x.User.FullName,
                ClientEmail = x.User.Email,
                SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                    x.PackageOrder.SelectedBudget ?? x.PackageOrder.Amount,
                    x.PackageOrder.AiStudioReserveAmount),
                Status = x.Status,
                PlanningMode = x.PlanningMode,
                AssignedAgentUserId = x.AssignedAgentUserId,
                AssignedAgentName = x.AssignedAgentUser != null ? x.AssignedAgentUser.FullName : null,
                AssignedAt = x.AssignedAt,
                UpdatedAt = x.UpdatedAt,
                CreatedAt = x.CreatedAt,
                HasBrief = x.CampaignBrief != null,
                HasRecommendation = x.CampaignRecommendations.Any(),
                LatestRecommendationStatus = x.CampaignRecommendations
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.Status)
                    .FirstOrDefault(),
                LatestRecommendationRationale = x.CampaignRecommendations
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.Rationale)
                    .FirstOrDefault(),
                LatestRecommendationTotalCost = x.CampaignRecommendations
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (decimal?)r.TotalCost)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        var items = campaigns.Select(campaign =>
        {
            var workflowCampaign = campaign.ToWorkflowCampaign();
            var stage = CampaignWorkflowPolicy.ResolveAgentQueueStage(workflowCampaign);
            var manualReviewRequired = ExtractManualReviewRequired(campaign.LatestRecommendationRationale);
            var isOverBudget = campaign.LatestRecommendationTotalCost.HasValue
                && campaign.LatestRecommendationTotalCost.Value > campaign.SelectedBudget
                && !string.Equals(campaign.LatestRecommendationStatus, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase);
            var updatedAt = new DateTimeOffset(campaign.UpdatedAt, TimeSpan.Zero);
            var ageInDays = Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - updatedAt).TotalDays));
            var isStale = CampaignWorkflowPolicy.IsAgentQueueStageStale(stage, updatedAt);
            var isUrgent = manualReviewRequired
                || isOverBudget
                || stage is QueueStages.PlanningReady or QueueStages.AgentReview
                || (stage == QueueStages.NewlyPaid && ageInDays >= 1)
                || (stage == QueueStages.WaitingOnClient && ageInDays >= 3)
                || isStale;

            return new AgentInboxItemResponse
            {
                Id = campaign.Id,
                UserId = campaign.UserId,
                CampaignName = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBandName} campaign" : campaign.CampaignName.Trim(),
                ClientName = campaign.ClientName,
                ClientEmail = campaign.ClientEmail,
                PackageBandId = campaign.PackageBandId,
                PackageBandName = campaign.PackageBandName,
                SelectedBudget = campaign.SelectedBudget,
                Status = campaign.Status,
                PlanningMode = campaign.PlanningMode,
                QueueStage = stage,
                QueueLabel = CampaignWorkflowPolicy.GetAgentQueueLabel(stage),
                AssignedAgentUserId = campaign.AssignedAgentUserId,
                AssignedAgentName = campaign.AssignedAgentName,
                AssignedAt = campaign.AssignedAt.HasValue ? new DateTimeOffset(campaign.AssignedAt.Value, TimeSpan.Zero) : null,
                IsAssignedToCurrentUser = campaign.AssignedAgentUserId == currentUserId,
                IsUnassigned = campaign.AssignedAgentUserId is null,
                NextAction = CampaignWorkflowPolicy.GetAgentNextAction(workflowCampaign, stage, currentUserId),
                ManualReviewRequired = manualReviewRequired,
                IsOverBudget = isOverBudget,
                IsStale = isStale,
                IsUrgent = isUrgent,
                AgeInDays = ageInDays,
                HasBrief = campaign.HasBrief,
                HasRecommendation = campaign.HasRecommendation,
                CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero),
                UpdatedAt = updatedAt
            };
        })
        .OrderByDescending(x => x.IsUrgent)
        .ThenByDescending(x => x.IsAssignedToCurrentUser)
        .ThenByDescending(x => x.IsUnassigned)
        .ThenBy(x => CampaignWorkflowPolicy.GetAgentQueueRank(x.QueueStage))
        .ThenByDescending(x => x.UpdatedAt)
        .ToArray();

        var response = new AgentInboxResponse
        {
            TotalCampaigns = items.Length,
            AssignedToMeCount = items.Count(x => x.IsAssignedToCurrentUser),
            UnassignedCount = items.Count(x => x.IsUnassigned),
            UrgentCount = items.Count(x => x.IsUrgent),
            ManualReviewCount = items.Count(x => x.ManualReviewRequired),
            OverBudgetCount = items.Count(x => x.IsOverBudget),
            StaleCount = items.Count(x => x.IsStale),
            NewlyPaidCount = items.Count(x => x.QueueStage == QueueStages.NewlyPaid),
            BriefWaitingCount = items.Count(x => x.QueueStage == QueueStages.BriefWaiting),
            PlanningReadyCount = items.Count(x => x.QueueStage == QueueStages.PlanningReady),
            AgentReviewCount = items.Count(x => x.QueueStage == QueueStages.AgentReview),
            ReadyToSendCount = items.Count(x => x.QueueStage == QueueStages.ReadyToSend),
            WaitingOnClientCount = items.Count(x => x.QueueStage == QueueStages.WaitingOnClient),
            CompletedCount = items.Count(x => x.QueueStage == QueueStages.Completed),
            Items = items
        };

        return Ok(response);
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var sales = await _db.Campaigns
            .AsNoTracking()
            .Where(x => x.AssignedAgentUserId == currentUserId && x.PackageOrder.PaymentStatus == "paid")
            .OrderByDescending(x => x.PackageOrder.PurchasedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AgentSaleItemResponse
            {
                CampaignId = x.Id,
                PackageOrderId = x.PackageOrderId,
                CampaignName = string.IsNullOrWhiteSpace(x.CampaignName) ? $"{x.PackageBand.Name} campaign" : x.CampaignName.Trim(),
                ClientName = x.User.FullName,
                ClientEmail = x.User.Email,
                PackageBandName = x.PackageBand.Name,
                SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                    x.PackageOrder.SelectedBudget ?? x.PackageOrder.Amount,
                    x.PackageOrder.AiStudioReserveAmount),
                ChargedAmount = x.PackageOrder.Amount,
                PaymentProvider = x.PackageOrder.PaymentProvider ?? "unknown",
                PaymentReference = x.PackageOrder.PaymentReference,
                ConvertedFromProspect = x.PackageOrder.PaymentProvider == "prospect",
                PurchasedAt = new DateTimeOffset(x.PackageOrder.PurchasedAt ?? x.CreatedAt, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(x.CreatedAt, TimeSpan.Zero)
            })
            .ToArrayAsync(cancellationToken);

        var response = new AgentSalesResponse
        {
            TotalSalesCount = sales.Length,
            ConvertedProspectSalesCount = sales.Count(x => x.ConvertedFromProspect),
            TotalChargedAmount = sales.Sum(x => x.ChargedAmount),
            TotalSelectedBudget = sales.Sum(x => x.SelectedBudget),
            Items = sales
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = campaign.ToDetail(currentUserId);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(campaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(campaign, queueStage, currentUserId);
        response.RecommendationPdfUrl = response.Recommendations.Count > 0
            ? $"/agent/campaigns/{campaign.Id}/recommendation-pdf"
            : null;

        if (string.IsNullOrWhiteSpace(response.CampaignName))
        {
            response.CampaignName = $"{campaign.PackageBand.Name} campaign";
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/recommendation-pdf")]
    public async Task<IActionResult> DownloadRecommendationPdf(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);

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

    [HttpPost("prospects")]
    public async Task<IActionResult> CreateProspectCampaign([FromBody] CreateProspectCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;

        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = (request.Email?.Trim() ?? string.Empty).ToLowerInvariant();
        var phone = request.Phone?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Prospect full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Prospect email is required.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new InvalidOperationException("Prospect phone is required.");
        }

        var packageBand = await _db.PackageBands
            .FirstOrDefaultAsync(x => x.Id == request.PackageBandId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Package band not found.");
        var selectedBudget = await ResolveProspectBudgetAsync(packageBand, cancellationToken);

        var user = await _db.UserAccounts
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        var createdNewUser = false;
        if (user is null)
        {
            var nowUtc = DateTime.UtcNow;
            user = new UserAccount
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Phone = phone,
                IsSaCitizen = true,
                Role = UserRole.Client,
                AccountStatus = AccountStatus.PendingVerification,
                EmailVerified = false,
                PhoneVerified = false,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            user.PasswordHash = _passwordHashingService.HashPassword(user, Guid.NewGuid().ToString("N"));
            _db.UserAccounts.Add(user);
            createdNewUser = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        var packageOrder = new PackageOrder
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PackageBandId = packageBand.Id,
            Amount = selectedBudget,
            SelectedBudget = selectedBudget,
            AiStudioReservePercent = 0m,
            AiStudioReserveAmount = 0m,
            Currency = "ZAR",
            PaymentProvider = "prospect",
            PaymentStatus = "pending",
            RefundStatus = "none",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PackageOrders.Add(packageOrder);

        var campaignName = string.IsNullOrWhiteSpace(request.CampaignName)
            ? $"{packageBand.Name} prospect campaign"
            : request.CampaignName.Trim();

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PackageOrderId = packageOrder.Id,
            PackageBandId = packageBand.Id,
            CampaignName = campaignName,
            Status = "awaiting_purchase",
            AiUnlocked = false,
            AgentAssistanceRequested = true,
            AssignedAgentUserId = currentUserId,
            AssignedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        var refreshedCampaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignAssets)
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == campaign.Id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        await WriteChangeAuditAsync(
            "create_prospect_campaign",
            "campaign",
            refreshedCampaign.Id.ToString(),
            ResolveCampaignLabel(refreshedCampaign),
            $"Created prospect campaign {ResolveCampaignLabel(refreshedCampaign)}.",
            new
            {
                CampaignId = refreshedCampaign.Id,
                ProspectEmail = email,
                PackageBand = packageBand.Name,
                SelectedBudget = selectedBudget,
                CreatedNewUser = createdNewUser
            },
            cancellationToken);

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        return Ok(response);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var agentUserId = request.AgentUserId ?? currentUserId;

        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        campaign.AssignedAgentUserId = agentUserId;
        campaign.AssignedAt = DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "assign",
            "campaign",
            campaign.Id.ToString(),
            campaign.CampaignName,
            $"Assigned campaign {ResolveCampaignLabel(campaign)}.",
            new { CampaignId = campaign.Id, AssignedAgentUserId = agentUserId },
            cancellationToken);
        await SendAssignmentEmailIfNeededAsync(campaign.Id, cancellationToken);

        return Accepted(new { CampaignId = id, AssignedAgentUserId = agentUserId, Message = "Campaign assigned." });
    }

    [HttpPost("{id:guid}/convert-to-sale")]
    public async Task<IActionResult> ConvertProspectToSale(Guid id, [FromBody] ConvertProspectToSaleRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (currentUser.Role == UserRole.Agent && campaign.AssignedAgentUserId != currentUser.Id)
        {
            throw new InvalidOperationException("Only the assigned agent can convert this campaign to a sale.");
        }

        if (!string.Equals(campaign.Status, "awaiting_purchase", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only prospective campaigns can be converted to a sale.");
        }

        if (string.Equals(campaign.PackageOrder.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            var paidCampaign = await _db.Campaigns
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.User)
                    .ThenInclude(x => x.BusinessProfile)
                .Include(x => x.AssignedAgentUser)
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
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new InvalidOperationException("Campaign not found.");

            return Ok(paidCampaign.ToDetail(currentUser.Id));
        }

        var paymentReference = NormalizeOptionalText(request.PaymentReference)
            ?? $"agent-sale-{DateTime.UtcNow:yyyyMMddHHmmss}-{campaign.Id.ToString("N")[..8]}";

        await _packagePurchaseService.MarkOrderPaidAsync(campaign.PackageOrderId, paymentReference, cancellationToken);

        var refreshedCampaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!refreshedCampaign.User.EmailVerified)
        {
            await _emailVerificationService.QueueActivationEmailAsync(refreshedCampaign.User, cancellationToken);
        }

        await WriteChangeAuditAsync(
            "convert_prospect_to_sale",
            "campaign",
            refreshedCampaign.Id.ToString(),
            ResolveCampaignLabel(refreshedCampaign),
            $"Converted prospect campaign {ResolveCampaignLabel(refreshedCampaign)} into a paid sale.",
            new
            {
                CampaignId = refreshedCampaign.Id,
                PackageOrderId = refreshedCampaign.PackageOrderId,
                paymentReference
            },
            cancellationToken);

        var response = refreshedCampaign.ToDetail(currentUser.Id);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUser.Id);
        return Ok(response);
    }

    [HttpPut("{id:guid}/prospect-pricing")]
    public async Task<IActionResult> UpdateProspectPricing(Guid id, [FromBody] UpdateProspectPricingRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (currentUser.Role == UserRole.Agent && campaign.AssignedAgentUserId != currentUser.Id)
        {
            throw new InvalidOperationException("Only the assigned agent can update this prospect campaign.");
        }

        if (!string.Equals(campaign.Status, "awaiting_purchase", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only prospective campaigns can be repriced.");
        }

        var packageBand = await _db.PackageBands
            .FirstOrDefaultAsync(x => x.Id == request.PackageBandId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Package band not found.");
        var selectedBudget = await ResolveProspectBudgetAsync(packageBand, cancellationToken);

        campaign.PackageBandId = packageBand.Id;
        campaign.PackageOrder.PackageBandId = packageBand.Id;
        campaign.PackageOrder.Amount = selectedBudget;
        campaign.PackageOrder.SelectedBudget = selectedBudget;
        campaign.PackageOrder.UpdatedAt = DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "update_prospect_pricing",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Updated prospect package band and budget for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                PackageBandId = packageBand.Id,
                PackageBandName = packageBand.Name,
                SelectedBudget = selectedBudget
            },
            cancellationToken);

        _db.ChangeTracker.Clear();
        var refreshedCampaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = refreshedCampaign.ToDetail(currentUser.Id);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUser.Id);
        return Ok(response);
    }

    [HttpPost("{id:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        campaign.AssignedAgentUserId = null;
        campaign.AssignedAt = null;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "unassign",
            "campaign",
            campaign.Id.ToString(),
            campaign.CampaignName,
            $"Unassigned campaign {ResolveCampaignLabel(campaign)}.",
            new { CampaignId = campaign.Id },
            cancellationToken);

        return Accepted(new { CampaignId = id, Message = "Campaign unassigned." });
    }

    [HttpPost("{id:guid}/mark-launched")]
    public async Task<IActionResult> MarkLaunched(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign is not ready to be marked live.",
                Detail = "Only campaigns with final creative approval captured can be activated as live.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        campaign.Status = CampaignStatuses.Launched;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "mark_launched",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Marked {ResolveCampaignLabel(campaign)} as live.",
            new { CampaignId = campaign.Id, campaign.Status },
            cancellationToken);
        await SendCampaignLaunchedEmailAsync(campaign, cancellationToken);

        return Accepted(new { CampaignId = id, Status = campaign.Status, Message = "Campaign marked live." });
    }

    [HttpPost("{id:guid}/assets")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<CampaignAssetResponse>> UploadAsset(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? assetType,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (file.Length <= 0)
        {
            return BadRequest(new { message = "Select a file to upload." });
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        await using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }

        var safeFileName = Path.GetFileName(file.FileName);
        var normalizedAssetType = string.IsNullOrWhiteSpace(assetType) ? "operations_asset" : assetType.Trim().ToLowerInvariant();
        var objectKey = $"campaigns/{campaign.Id:D}/assets/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{safeFileName}";
        var savedKey = await _assetStorage.SaveAsync(objectKey, bytes, file.ContentType ?? "application/octet-stream", cancellationToken);

        var asset = new CampaignAsset
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            UploadedByUserId = currentUser.Id,
            AssetType = normalizedAssetType,
            DisplayName = safeFileName,
            StorageObjectKey = savedKey,
            PublicUrl = _assetStorage.GetPublicUrl(savedKey),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        _db.CampaignAssets.Add(asset);
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "upload_campaign_asset",
            "campaign_asset",
            asset.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Uploaded {asset.DisplayName} for {ResolveCampaignLabel(campaign)}.",
            new { CampaignId = campaign.Id, AssetType = asset.AssetType, asset.DisplayName, asset.SizeBytes },
            cancellationToken);

        return Ok(new CampaignAssetResponse
        {
            Id = asset.Id,
            AssetType = asset.AssetType,
            DisplayName = asset.DisplayName,
            PublicUrl = asset.PublicUrl ?? $"/campaign-assets/{asset.Id}",
            ContentType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            CreatedAt = new DateTimeOffset(asset.CreatedAt, TimeSpan.Zero)
        });
    }

    [HttpPost("{id:guid}/supplier-bookings")]
    public async Task<IActionResult> SaveSupplierBooking(Guid id, [FromBody] SaveCampaignSupplierBookingRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (string.IsNullOrWhiteSpace(request.SupplierOrStation) || string.IsNullOrWhiteSpace(request.Channel))
        {
            return BadRequest(new { message = "Supplier/station and channel are required." });
        }

        var now = DateTime.UtcNow;
        var booking = new CampaignSupplierBooking
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            CreatedByUserId = currentUser.Id,
            ProofAssetId = request.ProofAssetId,
            SupplierOrStation = request.SupplierOrStation.Trim(),
            Channel = request.Channel.Trim(),
            BookingStatus = string.IsNullOrWhiteSpace(request.BookingStatus) ? "planned" : request.BookingStatus.Trim().ToLowerInvariant(),
            CommittedAmount = request.CommittedAmount,
            BookedAt = request.BookedAt?.UtcDateTime,
            LiveFrom = request.LiveFrom,
            LiveTo = request.LiveTo,
            Notes = NormalizeOptionalText(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CampaignSupplierBookings.Add(booking);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "save_supplier_booking",
            "campaign_supplier_booking",
            booking.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Saved supplier booking for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                booking.SupplierOrStation,
                booking.Channel,
                booking.BookingStatus,
                booking.CommittedAmount,
                booking.LiveFrom,
                booking.LiveTo
            },
            cancellationToken);

        await SendSupplierBookingEmailAsync(campaign, booking, cancellationToken);
        return Accepted(new { CampaignId = campaign.Id, SupplierBookingId = booking.Id, Message = "Supplier booking saved." });
    }

    [HttpPost("{id:guid}/delivery-reports")]
    public async Task<IActionResult> SaveDeliveryReport(Guid id, [FromBody] SaveCampaignDeliveryReportRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (string.IsNullOrWhiteSpace(request.ReportType) || string.IsNullOrWhiteSpace(request.Headline))
        {
            return BadRequest(new { message = "Report type and headline are required." });
        }

        var now = DateTime.UtcNow;
        var report = new CampaignDeliveryReport
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            SupplierBookingId = request.SupplierBookingId,
            EvidenceAssetId = request.EvidenceAssetId,
            CreatedByUserId = currentUser.Id,
            ReportType = request.ReportType.Trim().ToLowerInvariant(),
            Headline = request.Headline.Trim(),
            Summary = NormalizeOptionalText(request.Summary),
            ReportedAt = request.ReportedAt?.UtcDateTime ?? now,
            Impressions = request.Impressions,
            PlaysOrSpots = request.PlaysOrSpots,
            SpendDelivered = request.SpendDelivered,
            CreatedAt = now
        };

        _db.CampaignDeliveryReports.Add(report);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "save_delivery_report",
            "campaign_delivery_report",
            report.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Saved delivery report for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                report.ReportType,
                report.Headline,
                report.Impressions,
                report.PlaysOrSpots,
                report.SpendDelivered
            },
            cancellationToken);

        await SendDeliveryReportEmailAsync(campaign, report, cancellationToken);
        return Accepted(new { CampaignId = campaign.Id, DeliveryReportId = report.Id, Message = "Delivery report saved." });
    }

    [HttpPost("{id:guid}/recommendations")]
    public async Task<IActionResult> CreateRecommendation(Guid id, [FromBody] AgentRecommendationRequest request, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var now = DateTime.UtcNow;
        var recommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.RevisionNumber)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (recommendation is null)
        {
            var revisionNumber = RecommendationRevisionSupport.GetNextRevisionNumber(campaign.CampaignRecommendations);
            recommendation = new CampaignRecommendation
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                RecommendationType = "agent_assisted",
                GeneratedBy = "agent",
                Status = RecommendationStatuses.Draft,
                TotalCost = PricingPolicy.ResolvePlanningBudget(
                    campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                    campaign.PackageOrder.AiStudioReserveAmount),
                Summary = request.Notes,
                Rationale = request.Notes,
                RevisionNumber = revisionNumber,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.CampaignRecommendations.Add(recommendation);
        }
        else
        {
            recommendation.Summary = request.Notes;
            recommendation.Rationale = MergeClientFeedback(request.Notes, ExtractClientFeedbackNotes(recommendation.Rationale));
            recommendation.UpdatedAt = now;
        }

        try
        {
            RecommendationOohPolicy.EnsureSelectedInventoryContainsOoh(request.InventoryItems);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        SyncRecommendationItems(recommendation, request.InventoryItems, now);
        recommendation.TotalCost = recommendation.RecommendationItems.Sum(x => x.TotalCost);
        if (recommendation.TotalCost <= 0)
        {
            recommendation.TotalCost = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount);
        }

        campaign.Status = CampaignStatuses.PlanningInProgress;
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "save_recommendation",
            "recommendation",
            recommendation.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Saved recommendation draft for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                RecommendationId = recommendation.Id,
                recommendation.RevisionNumber,
                ItemCount = recommendation.RecommendationItems.Count,
                recommendation.TotalCost
            },
            cancellationToken);
        await SendAgentWorkStartedEmailIfNeededAsync(campaign.Id, cancellationToken);

        return Accepted(new { CampaignId = id, RecommendationId = recommendation.Id, Message = "Recommendation saved." });
    }

    [HttpPost("{id:guid}/initialize-recommendation")]
    public async Task<IActionResult> InitializeRecommendation(Guid id, [FromBody] InitializeRecommendationFlowRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
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
            else if (string.Equals(campaign.Status, "awaiting_purchase", StringComparison.OrdinalIgnoreCase))
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

        _db.ChangeTracker.Clear();
        var refreshedCampaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignAssets)
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        if (string.IsNullOrWhiteSpace(response.CampaignName))
        {
            response.CampaignName = $"{refreshedCampaign.PackageBand.Name} campaign";
        }

        await WriteChangeAuditAsync(
            request.SubmitBrief ? "submit_brief" : "save_brief_draft",
            "campaign",
            refreshedCampaign.Id.ToString(),
            response.CampaignName,
            request.SubmitBrief
                ? $"Submitted campaign brief for {response.CampaignName}."
                : $"Saved campaign brief draft for {response.CampaignName}.",
            new
            {
                CampaignId = refreshedCampaign.Id,
                request.SubmitBrief,
                request.PlanningMode
            },
            cancellationToken);

        return Ok(response);
    }

    private static void MapBrief(CampaignBrief brief, SaveCampaignBriefRequest request, DateTime now)
    {
        var normalizedScope = NormalizeGeographyScope(request.GeographyScope);
        var normalizedProvinces = NormalizeScopeList(request.Provinces, normalizedScope == "provincial");
        var normalizedCities = NormalizeScopeList(request.Cities, normalizedScope == "local");
        var normalizedSuburbs = NormalizeScopeList(request.Suburbs, normalizedScope == "local");
        var normalizedAreas = NormalizeScopeList(request.Areas, normalizedScope == "local");

        brief.Objective = request.Objective;
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

        _db.ChangeTracker.Clear();
        var refreshedCampaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignAssets)
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        if (string.IsNullOrWhiteSpace(response.CampaignName))
        {
            response.CampaignName = $"{refreshedCampaign.PackageBand.Name} campaign";
        }

        var latestRecommendation = refreshedCampaign.CampaignRecommendations
            .OrderByDescending(x => x.RevisionNumber)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        await WriteChangeAuditAsync(
            "generate_recommendation",
            "campaign",
            refreshedCampaign.Id.ToString(),
            response.CampaignName,
            $"Generated recommendation set for {response.CampaignName}.",
            new
            {
                CampaignId = refreshedCampaign.Id,
                RecommendationId = latestRecommendation?.Id,
                RevisionNumber = latestRecommendation?.RevisionNumber,
                Status = latestRecommendation?.Status
            },
            cancellationToken);

        return Ok(response);
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

    [HttpPost("{id:guid}/send-to-client")]
    public async Task<IActionResult> SendToClient(Guid id, [FromBody] SendToClientRequest request, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }

        try
        {
            foreach (var recommendation in currentRecommendations)
            {
                RecommendationOohPolicy.EnsureRecommendationContainsOoh(recommendation.RecommendationItems);
            }
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }

        foreach (var recommendation in currentRecommendations)
        {
            recommendation.Status = RecommendationStatuses.SentToClient;
            recommendation.SentToClientAt = DateTime.UtcNow;
            recommendation.UpdatedAt = DateTime.UtcNow;
        }

        campaign.Status = CampaignStatuses.ReviewReady;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "send_to_client",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Sent recommendation set to client for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                ProposalCount = currentRecommendations.Length,
                request.Message
            },
            cancellationToken);

        await SendRecommendationReadyEmailIfNeededAsync(campaign.Id, currentRecommendations, request.Message, cancellationToken);

        return Accepted(new { CampaignId = id, ProposalCount = currentRecommendations.Length, Message = "Recommendation set sent to client.", ClientMessage = request.Message });
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

    private sealed class AgentCampaignListProjection
    {
        public Guid Id { get; init; }
        public Guid PackageOrderId { get; init; }
        public Guid PackageBandId { get; init; }
        public string PackageBandName { get; init; } = string.Empty;
        public decimal SelectedBudget { get; init; }
        public string? CampaignName { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? PlanningMode { get; init; }
        public bool AiUnlocked { get; init; }
        public bool AgentAssistanceRequested { get; init; }
        public Guid? AssignedAgentUserId { get; init; }
        public string? AssignedAgentName { get; init; }
        public DateTime? AssignedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class AgentInboxProjection
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public string? CampaignName { get; init; }
        public Guid PackageBandId { get; init; }
        public string PackageBandName { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string ClientEmail { get; init; } = string.Empty;
        public decimal SelectedBudget { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? PlanningMode { get; init; }
        public Guid? AssignedAgentUserId { get; init; }
        public string? AssignedAgentName { get; init; }
        public DateTime? AssignedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool HasBrief { get; init; }
        public bool HasRecommendation { get; init; }
        public string? LatestRecommendationStatus { get; init; }
        public string? LatestRecommendationRationale { get; init; }
        public decimal? LatestRecommendationTotalCost { get; init; }

        public Campaign ToWorkflowCampaign()
        {
            var campaign = new Campaign
            {
                Id = Id,
                UserId = UserId,
                CampaignName = CampaignName ?? string.Empty,
                Status = Status,
                PlanningMode = PlanningMode,
                AssignedAgentUserId = AssignedAgentUserId,
                AssignedAt = AssignedAt,
                UpdatedAt = UpdatedAt,
                CreatedAt = CreatedAt,
                PackageBand = new PackageBand
                {
                    Name = PackageBandName
                },
                PackageOrder = new PackageOrder
                {
                    Amount = SelectedBudget,
                    SelectedBudget = SelectedBudget
                },
                AssignedAgentUser = string.IsNullOrWhiteSpace(AssignedAgentName)
                    ? null
                    : new UserAccount { FullName = AssignedAgentName.Trim() },
                CampaignRecommendations = BuildRecommendations()
            };

            return campaign;
        }

        private List<CampaignRecommendation> BuildRecommendations()
        {
            if (!HasRecommendation)
            {
                return new List<CampaignRecommendation>();
            }

            return new List<CampaignRecommendation>
            {
                new()
                {
                    Status = LatestRecommendationStatus ?? string.Empty,
                    Rationale = LatestRecommendationRationale,
                    TotalCost = LatestRecommendationTotalCost ?? 0m,
                    CreatedAt = UpdatedAt
                }
            };
        }
    }

    private async Task<UserAccount> GetCurrentOperationsUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (currentUser.Role is not UserRole.Agent and not UserRole.Admin)
        {
            throw new InvalidOperationException("Agent or admin access is required.");
        }

        return currentUser;
    }

    private static string ResolveCampaignLabel(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand?.Name ?? "Campaign"} campaign"
            : campaign.CampaignName.Trim();
    }

    private static void SyncRecommendationItems(CampaignRecommendation recommendation, IReadOnlyList<SelectedInventoryItemRequest> inventoryItems, DateTime now)
    {
        recommendation.RecommendationItems.Clear();

        foreach (var item in inventoryItems)
        {
            var quantity = item.Quantity <= 0 ? 1 : item.Quantity;
            recommendation.RecommendationItems.Add(new RecommendationItem
            {
                Id = Guid.NewGuid(),
                RecommendationId = recommendation.Id,
                InventoryType = string.IsNullOrWhiteSpace(item.Type) ? "base" : item.Type.Trim().ToLowerInvariant(),
                DisplayName = string.IsNullOrWhiteSpace(item.Station) ? "Selected inventory" : item.Station.Trim(),
                Quantity = quantity,
                UnitCost = item.Rate,
                TotalCost = item.Rate * quantity,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    sourceInventoryId = item.Id,
                    rationale = BuildInventoryRationale(item),
                    region = item.Region,
                    language = item.Language,
                    showDaypart = item.ShowDaypart,
                    timeBand = item.TimeBand,
                    slotType = item.SlotType,
                    duration = item.Duration,
                    restrictions = item.Restrictions,
                    quantity,
                    flighting = item.Flighting,
                    itemNotes = item.Notes,
                    startDate = item.StartDate,
                    endDate = item.EndDate
                }),
                CreatedAt = now
            });
        }
    }

    private static string BuildInventoryRationale(SelectedInventoryItemRequest item)
    {
        var quantity = item.Quantity <= 0 ? 1 : item.Quantity;
        var parts = new List<string>
        {
            item.Region,
            item.Language,
            item.ShowDaypart,
            item.TimeBand,
            item.SlotType,
            item.Duration,
            $"Qty {quantity}",
            item.Restrictions
        };

        if (!string.IsNullOrWhiteSpace(item.Flighting))
        {
            parts.Add($"Flighting: {item.Flighting.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(item.Notes))
        {
            parts.Add($"Notes: {item.Notes.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(item.StartDate) || !string.IsNullOrWhiteSpace(item.EndDate))
        {
            parts.Add($"Dates: {(item.StartDate ?? "-")} to {(item.EndDate ?? "-")}");
        }

        return string.Join(" | ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? ExtractClientFeedbackNotes(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        var markerIndex = rationale.LastIndexOf(ClientFeedbackMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var notes = rationale[(markerIndex + ClientFeedbackMarker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
    }

    private static bool ExtractManualReviewRequired(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return false;
        }

        var line = rationale
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .LastOrDefault(entry => entry.StartsWith(ManualReviewMarker, StringComparison.OrdinalIgnoreCase));

        if (line is null)
        {
            return false;
        }

        var rawValue = line[(ManualReviewMarker.Length)..].Trim();
        return bool.TryParse(rawValue, out var parsed) && parsed;
    }

    private static string MergeClientFeedback(string notes, string? clientFeedbackNotes)
    {
        if (string.IsNullOrWhiteSpace(clientFeedbackNotes))
        {
            return notes;
        }

        return $"{notes.Trim()}\n\n{ClientFeedbackMarker} {clientFeedbackNotes}".Trim();
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private string BuildProposalUrl(Guid campaignId, Guid? recommendationId = null, string? action = null)
    {
        var accessToken = _proposalAccessTokenService.CreateToken(campaignId);
        var query = new List<string>
        {
            $"token={Uri.EscapeDataString(accessToken)}"
        };

        if (recommendationId.HasValue)
        {
            query.Add($"recommendationId={Uri.EscapeDataString(recommendationId.Value.ToString("D"))}");
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query.Add($"action={Uri.EscapeDataString(action)}");
        }

        return BuildFrontendUrl($"/proposal/{campaignId:D}?{string.Join("&", query)}");
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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

    private async Task SendRecommendationReadyEmailIfNeededAsync(
        Guid campaignId,
        IReadOnlyList<CampaignRecommendation> recommendations,
        string? agentMessage,
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.RecommendationReadyEmailSentAt.HasValue)
        {
            return;
        }

        try
        {
            EmailAttachment[]? attachments = null;
            string recommendationPackBlock = string.Empty;
            var proposalCount = recommendations.Count;

            try
            {
                var recommendationPdfs = new List<EmailAttachment>();
                foreach (var recommendation in recommendations)
                {
                    var pdfBytes = await _recommendationDocumentService.GetRecommendationPdfBytesAsync(campaign.Id, recommendation.Id, cancellationToken);
                    recommendationPdfs.Add(new EmailAttachment
                    {
                        FileName = BuildRecommendationAttachmentFileName(campaign.Id, recommendation),
                        ContentType = "application/pdf",
                        Content = pdfBytes
                    });
                }

                attachments = recommendationPdfs.ToArray();
                recommendationPackBlock = @"
                    <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                      We have attached a separate PDF for each recommendation option. Each PDF starts with a one-page summary followed by the full detailed media plan.
                    </p>";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build recommendation PDF attachment for campaign {CampaignId}.", campaign.Id);
            }

            await _emailService.SendAsync(
                "recommendation-ready",
                campaign.User.Email,
                "noreply",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.User.FullName,
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["BudgetLabel"] = ResolveBudgetLabel(campaign),
                    ["Budget"] = ResolveBudgetDisplayText(campaign),
                    ["ReviewUrl"] = BuildProposalUrl(campaign.Id),
                    ["ProposalCount"] = proposalCount.ToString(CultureInfo.InvariantCulture),
                    ["ProposalSummary"] = proposalCount > 1
                        ? $"We have prepared {proposalCount} proposal options for you to compare."
                        : "We have prepared your recommendation for review.",
                    ["AgentMessageBlock"] = BuildAgentMessageBlock(agentMessage),
                    ["RecommendationPackBlock"] = recommendationPackBlock,
                    ["ProposalAcceptButtonsBlock"] = BuildProposalAcceptButtonsBlock(campaign.Id, recommendations)
                },
                attachments,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recommendation ready email for campaign {CampaignId}.", campaign.Id);
            return;
        }

        campaign.RecommendationReadyEmailSentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildRecommendationAttachmentFileName(Guid campaignId, CampaignRecommendation recommendation)
    {
        var rawProposalLabel = recommendation.RecommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()
            ?? $"proposal-{recommendation.Id:D}";

        var safeProposalLabel = new string(rawProposalLabel
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(safeProposalLabel))
        {
            safeProposalLabel = $"proposal-{recommendation.Id:D}";
        }

        return $"advertified-recommendation-{campaignId:D}-{safeProposalLabel}.pdf";
    }

    private static string ResolveBudgetLabel(Campaign campaign)
    {
        return string.Equals(campaign.Status, "awaiting_purchase", StringComparison.OrdinalIgnoreCase)
            ? "Package range"
            : "Selected budget";
    }

    private static string ResolveBudgetDisplayText(Campaign campaign)
    {
        if (string.Equals(campaign.Status, "awaiting_purchase", StringComparison.OrdinalIgnoreCase))
        {
            return $"{FormatCurrency(campaign.PackageBand.MinBudget)} to {FormatCurrency(campaign.PackageBand.MaxBudget)}";
        }

        return FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount);
    }

    private string BuildProposalAcceptButtonsBlock(Guid campaignId, IReadOnlyList<CampaignRecommendation> recommendations)
    {
        if (recommendations.Count == 0)
        {
            return string.Empty;
        }

        var links = recommendations
            .Select((recommendation, index) =>
            {
                var label = ResolveProposalLabel(recommendation.RecommendationType, index);
                var href = BuildProposalUrl(campaignId, recommendation.Id);
                return $@"<a href=""{href}"" style=""display:inline-block;margin:0 10px 10px 0;padding:11px 16px;border-radius:12px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">Accept {System.Net.WebUtility.HtmlEncode(label)}</a>";
            })
            .ToArray();
        var rejectHref = BuildProposalUrl(campaignId, null, "reject_all");
        var rejectLink = $@"<a href=""{rejectHref}"" style=""display:inline-block;margin:0 10px 10px 0;padding:11px 16px;border-radius:12px;border:1px solid #cbd5e1;background:#ffffff;color:#123A33;text-decoration:none;font-weight:700;"">Request new proposals</a>";

        return $@"
            <div style=""margin:12px 0 4px 0;"">
              <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;margin-bottom:8px;"">Quick accept</div>
              {string.Join(string.Empty, links)}
              {rejectLink}
            </div>";
    }

    private static string ResolveProposalLabel(string? recommendationType, int proposalIndex)
    {
        var variantKey = recommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => "Proposal A",
            "ooh_focus" => "Proposal B",
            "radio_focus" => "Proposal C",
            "digital_focus" => "Proposal C",
            "tv_focus" => "Proposal C",
            _ => $"Proposal {GetProposalLetter(proposalIndex)}"
        };
    }

    private static string GetProposalLetter(int index)
    {
        return index >= 0 && index < 26
            ? ((char)('A' + index)).ToString()
            : (index + 1).ToString();
    }

    private static bool IsProspectiveCampaign(Campaign campaign)
    {
        return !CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder)
            || string.Equals(campaign.Status, "awaiting_purchase", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendCampaignLaunchedEmailAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "campaign-live",
                campaign.User.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.User.FullName,
                    ["CampaignName"] = ResolveCampaignLabel(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["Budget"] = FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send campaign live email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendSupplierBookingEmailAsync(Campaign campaign, CampaignSupplierBooking booking, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "campaign-booking-confirmed",
                campaign.User.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.User.FullName,
                    ["CampaignName"] = ResolveCampaignLabel(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["SupplierOrStation"] = booking.SupplierOrStation,
                    ["Channel"] = booking.Channel,
                    ["BookingStatus"] = booking.BookingStatus,
                    ["CommittedAmount"] = FormatCurrency(booking.CommittedAmount),
                    ["LiveWindow"] = FormatLiveWindow(booking.LiveFrom, booking.LiveTo),
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send supplier booking email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendDeliveryReportEmailAsync(Campaign campaign, CampaignDeliveryReport report, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "campaign-report-available",
                campaign.User.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.User.FullName,
                    ["CampaignName"] = ResolveCampaignLabel(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["ReportType"] = report.ReportType,
                    ["Headline"] = report.Headline,
                    ["ReportedAt"] = report.ReportedAt?.ToString("u", CultureInfo.InvariantCulture) ?? DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture),
                    ["Impressions"] = report.Impressions?.ToString("N0", CultureInfo.InvariantCulture) ?? "Not supplied",
                    ["PlaysOrSpots"] = report.PlaysOrSpots?.ToString(CultureInfo.InvariantCulture) ?? "Not supplied",
                    ["SpendDelivered"] = report.SpendDelivered.HasValue ? FormatCurrency(report.SpendDelivered.Value) : "Not supplied",
                    ["CampaignUrl"] = BuildFrontendUrl($"/campaigns/{campaign.Id}")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send delivery report email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task<decimal> ResolveProspectBudgetAsync(PackageBand packageBand, CancellationToken cancellationToken)
    {
        var recommendedSpend = await _db.PackageBandProfiles
            .AsNoTracking()
            .Where(x => x.PackageBandId == packageBand.Id)
            .Select(x => x.RecommendedSpend)
            .FirstOrDefaultAsync(cancellationToken);

        if (recommendedSpend.HasValue && recommendedSpend.Value >= packageBand.MinBudget && recommendedSpend.Value <= packageBand.MaxBudget)
        {
            return recommendedSpend.Value;
        }

        return decimal.Round((packageBand.MinBudget + packageBand.MaxBudget) / 2m, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildAgentMessageBlock(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var encodedMessage = System.Net.WebUtility.HtmlEncode(message.Trim())
            .Replace(Environment.NewLine, "<br/>")
            .Replace("\n", "<br/>");

        return $@"
            <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#ffffff;"">
              <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Message from your strategist</div>
              <p style=""margin:10px 0 0;font-size:15px;line-height:1.7;color:#4b635a;"">{encodedMessage}</p>
            </div>";
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }

    private static string FormatLiveWindow(DateOnly? liveFrom, DateOnly? liveTo)
    {
        if (liveFrom is null && liveTo is null)
        {
            return "Not scheduled yet";
        }

        if (liveFrom is not null && liveTo is not null)
        {
            return $"{liveFrom.Value:dd MMM yyyy} to {liveTo.Value:dd MMM yyyy}";
        }

        return liveFrom is not null ? $"{liveFrom.Value:dd MMM yyyy} onward" : $"Until {liveTo:dd MMM yyyy}";
    }
}

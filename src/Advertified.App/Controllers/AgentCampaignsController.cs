using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

/// <summary>
/// Handles read-only agent campaign operations: listing, querying, and viewing campaign details.
/// For write operations, see dedicated controllers: AgentCampaignWorkflowController, AgentProspectsController, etc.
/// </summary>
[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentCampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly ICampaignPlanningTargetResolver _planningTargetResolver;
    private readonly ICampaignBusinessLocationResolver _businessLocationResolver;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly ILogger<AgentCampaignsController> _logger;

    public AgentCampaignsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IRecommendationDocumentService recommendationDocumentService,
        ICampaignPlanningTargetResolver planningTargetResolver,
        ICampaignBusinessLocationResolver businessLocationResolver,
        IAgentCampaignOwnershipService ownershipService,
        ILogger<AgentCampaignsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _recommendationDocumentService = recommendationDocumentService;
        _planningTargetResolver = planningTargetResolver;
        _businessLocationResolver = businessLocationResolver;
        _ownershipService = ownershipService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var campaigns = await _ownershipService.ApplyReadableScope(_db.Campaigns, currentUser)
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AgentCampaignListProjection
            {
                Id = x.Id,
                UserId = x.UserId,
                ClientName = x.User != null ? x.User.FullName : x.ProspectLead != null ? x.ProspectLead.FullName : string.Empty,
                ClientEmail = x.User != null ? x.User.Email : x.ProspectLead != null ? x.ProspectLead.Email : string.Empty,
                BusinessName = x.User != null && x.User.BusinessProfile != null ? x.User.BusinessProfile.BusinessName : null,
                PackageOrderId = x.PackageOrderId,
                PackageBandId = x.PackageBandId,
                PackageBandName = x.PackageBand.Name,
                SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                    x.PackageOrder.SelectedBudget ?? x.PackageOrder.Amount,
                    x.PackageOrder.AiStudioReserveAmount),
                PaymentProvider = x.PackageOrder.PaymentProvider ?? string.Empty,
                PaymentStatus = x.PackageOrder.PaymentStatus,
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
            UserId = x.UserId,
            ClientName = x.ClientName,
            ClientEmail = x.ClientEmail,
            BusinessName = x.BusinessName,
            PackageOrderId = x.PackageOrderId,
            PackageBandId = x.PackageBandId,
            PackageBandName = x.PackageBandName,
            SelectedBudget = x.SelectedBudget,
            PaymentProvider = x.PaymentProvider,
            PaymentStatus = x.PaymentStatus,
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
        var campaigns = await _ownershipService.ApplyReadableScope(_db.Campaigns, currentUser)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.ProspectDispositionStatus != ProspectDispositionStatuses.Closed)
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var items = campaigns.Select(campaign =>
        {
            var workflowCampaign = campaign;
            var stage = CampaignWorkflowPolicy.ResolveAgentQueueStage(workflowCampaign);
            var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
            var activeRecommendation = currentRecommendations
                .FirstOrDefault(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase))
                ?? currentRecommendations.FirstOrDefault(x => string.Equals(x.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase))
                ?? currentRecommendations.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            var selectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount);
            var manualReviewRequired = ExtractManualReviewRequired(activeRecommendation?.Rationale);
            var isOverBudget = activeRecommendation is not null
                && activeRecommendation.TotalCost > selectedBudget
                && !string.Equals(activeRecommendation.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase);
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
                CampaignName = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                ClientName = campaign.ResolveClientName(),
                ClientEmail = campaign.ResolveClientEmail(),
                PackageBandId = campaign.PackageBandId,
                PackageBandName = campaign.PackageBand.Name,
                SelectedBudget = selectedBudget,
                PaymentStatus = campaign.PackageOrder.PaymentStatus,
                Status = campaign.Status,
                PlanningMode = campaign.PlanningMode,
                QueueStage = stage,
                QueueLabel = CampaignWorkflowPolicy.GetAgentQueueLabel(stage),
                AssignedAgentUserId = campaign.AssignedAgentUserId,
                AssignedAgentName = campaign.AssignedAgentUser?.FullName,
                AssignedAt = campaign.AssignedAt.HasValue ? new DateTimeOffset(campaign.AssignedAt.Value, TimeSpan.Zero) : null,
                IsAssignedToCurrentUser = campaign.AssignedAgentUserId == currentUserId,
                IsUnassigned = campaign.AssignedAgentUserId is null,
                NextAction = CampaignWorkflowPolicy.GetAgentNextAction(workflowCampaign, stage, currentUserId),
                ManualReviewRequired = manualReviewRequired,
                IsOverBudget = isOverBudget,
                IsStale = isStale,
                IsUrgent = isUrgent,
                AgeInDays = ageInDays,
                HasBrief = campaign.CampaignBrief != null,
                HasRecommendation = currentRecommendations.Length > 0,
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
        var sales = await _ownershipService.ApplyReadableScope(_db.Campaigns, currentUser)
            .AsNoTracking()
            .Where(x => x.PackageOrder.PaymentStatus == "paid")
            .OrderByDescending(x => x.PackageOrder.PurchasedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AgentSaleItemResponse
            {
                CampaignId = x.Id,
                PackageOrderId = x.PackageOrderId,
                CampaignName = string.IsNullOrWhiteSpace(x.CampaignName) ? $"{x.PackageBand.Name} campaign" : x.CampaignName.Trim(),
                ClientName = x.User != null ? x.User.FullName : x.ProspectLead != null ? x.ProspectLead.FullName : string.Empty,
                ClientEmail = x.User != null ? x.User.Email : x.ProspectLead != null ? x.ProspectLead.Email : string.Empty,
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
        var campaign = await _ownershipService.GetReadableCampaignAsync(
            id,
            currentUser,
            query => query
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.User!)
                    .ThenInclude(x => x.BusinessProfile)
                .Include(x => x.ProspectLead)
                .Include(x => x.AssignedAgentUser)
                .Include(x => x.ProspectDispositionClosedByUser)
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
                .Include(x => x.EmailDeliveryMessages),
            cancellationToken);

        var response = campaign.ToDetail(currentUserId);
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

    [HttpGet("{id:guid}/performance")]
    public async Task<ActionResult<CampaignPerformanceSnapshotResponse>> GetPerformance(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetReadableCampaignAsync(
            id,
            currentUser,
            query => query
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.CampaignChannelMetrics)
                .Include(x => x.CampaignSupplierBookings)
                .Include(x => x.CampaignDeliveryReports),
            cancellationToken);

        return Ok(campaign.ToPerformanceSnapshot());
    }

    [HttpGet("{id:guid}/recommendation-pdf")]
    public async Task<IActionResult> DownloadRecommendationPdf(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        _ = await _ownershipService.GetReadableCampaignAsync(
            id,
            currentUser,
            query => query.AsNoTracking(),
            cancellationToken);

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

    private sealed class AgentCampaignListProjection
    {
        public Guid Id { get; init; }
        public Guid? UserId { get; init; }
        public string? ClientName { get; init; }
        public string? ClientEmail { get; init; }
        public string? BusinessName { get; init; }
        public Guid PackageOrderId { get; init; }
        public Guid PackageBandId { get; init; }
        public string PackageBandName { get; init; } = string.Empty;
        public decimal SelectedBudget { get; init; }
        public string PaymentProvider { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
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

    private static bool ExtractManualReviewRequired(string? rationale)
    {
        const string ManualReviewMarker = "Manual review required:";
        
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
}

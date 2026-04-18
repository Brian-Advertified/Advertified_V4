using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Agent;
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
/// Handles prospect campaign operations: creation from cold leads and pricing adjustments.
/// Prospect campaigns represent potential sales that haven't yet been converted to paid orders.
/// </summary>
[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentProspectsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly IProspectLeadRegistrationService _prospectLeadRegistrationService;
    private readonly ILogger<AgentProspectsController> _logger;

    public AgentProspectsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAgentCampaignOwnershipService ownershipService,
        IChangeAuditService changeAuditService,
        IProspectLeadRegistrationService prospectLeadRegistrationService,
        ILogger<AgentProspectsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _ownershipService = ownershipService;
        _changeAuditService = changeAuditService;
        _prospectLeadRegistrationService = prospectLeadRegistrationService;
        _logger = logger;
    }

    [HttpPost("prospects")]
    public async Task<IActionResult> CreateProspectCampaign([FromBody] CreateProspectCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;

        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = ProspectLeadContactNormalizer.NormalizeEmail(request.Email);
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
            ?? throw new NotFoundException("Package band not found.");
        var selectedBudget = ResolveProspectBudget(packageBand);

        var leadResult = await _prospectLeadRegistrationService.UpsertAgentLeadAsync(
            currentUserId,
            fullName,
            email,
            phone,
            "agent_prospect",
            cancellationToken);
        var lead = leadResult.Lead;

        var now = DateTime.UtcNow;
        var packageOrder = new PackageOrder
        {
            Id = Guid.NewGuid(),
            ProspectLeadId = lead.Id,
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
            ProspectLeadId = lead.Id,
            PackageOrderId = packageOrder.Id,
            PackageBandId = packageBand.Id,
            CampaignName = campaignName,
            Status = CampaignStatuses.AwaitingPurchase,
            AiUnlocked = false,
            AgentAssistanceRequested = true,
            AssignedAgentUserId = currentUserId,
            AssignedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        var refreshedCampaign = await LoadCampaignDetailAsync(campaign.Id, cancellationToken);

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
                CreatedNewLead = leadResult.CreatedNewLead
            },
            cancellationToken);

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        return Ok(response);
    }

    [HttpPost("registered-prospects")]
    public async Task<IActionResult> CreateRegisteredClientProspectCampaign([FromBody] CreateRegisteredClientCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;

        var email = (request.Email?.Trim() ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Client email is required.");
        }

        var client = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken)
            ?? throw new NotFoundException("Client account not found.");

        if (client.Role != UserRole.Client)
        {
            throw new InvalidOperationException("Only client accounts can be converted to prospect campaigns.");
        }

        var packageBand = await _db.PackageBands
            .FirstOrDefaultAsync(x => x.Id == request.PackageBandId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Package band not found.");
        var selectedBudget = ResolveProspectBudget(packageBand);

        var now = DateTime.UtcNow;
        var packageOrder = new PackageOrder
        {
            Id = Guid.NewGuid(),
            UserId = client.Id,
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
            UserId = client.Id,
            PackageOrderId = packageOrder.Id,
            PackageBandId = packageBand.Id,
            CampaignName = campaignName,
            Status = CampaignStatuses.AwaitingPurchase,
            AiUnlocked = false,
            AgentAssistanceRequested = true,
            AssignedAgentUserId = currentUserId,
            AssignedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        var refreshedCampaign = await LoadCampaignDetailAsync(campaign.Id, cancellationToken);

        await WriteChangeAuditAsync(
            "create_registered_client_prospect_campaign",
            "campaign",
            refreshedCampaign.Id.ToString(),
            ResolveCampaignLabel(refreshedCampaign),
            $"Created prospect campaign {ResolveCampaignLabel(refreshedCampaign)} for registered client {email}.",
            new
            {
                CampaignId = refreshedCampaign.Id,
                ClientEmail = email,
                PackageBand = packageBand.Name,
                SelectedBudget = selectedBudget
            },
            cancellationToken);

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        return Ok(response);
    }

    [HttpPut("{id:guid}/prospect-pricing")]
    public async Task<IActionResult> UpdateProspectPricing(Guid id, [FromBody] UpdateProspectPricingRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.PackageOrder)
                .Include(x => x.PackageBand)
                .Include(x => x.User)
                .Include(x => x.ProspectLead),
            cancellationToken);

        if (!ProspectCampaignPolicy.IsProspectiveCampaign(campaign))
        {
            throw new InvalidOperationException("Only prospective campaigns can be repriced.");
        }

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            throw new InvalidOperationException("Reopen this prospect before changing package pricing.");
        }

        var packageBand = await _db.PackageBands
            .FirstOrDefaultAsync(x => x.Id == request.PackageBandId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Package band not found.");
        var selectedBudget = ResolveProspectBudget(packageBand);

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
        var refreshedCampaign = await LoadCampaignDetailAsync(id, cancellationToken);

        var response = refreshedCampaign.ToDetail(currentUser.Id);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUser.Id);
        return Ok(response);
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

    private static string ResolveCampaignLabel(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand?.Name ?? "Campaign"} campaign"
            : campaign.CampaignName.Trim();
    }

    private async Task<Campaign> LoadCampaignDetailAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.Campaigns
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
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");
    }

    private static decimal ResolveProspectBudget(PackageBand packageBand)
    {
        // Use the package floor until the client or agent confirms an exact spend.
        return packageBand.MinBudget > 0m ? packageBand.MinBudget : 25000m;
    }
}

using System.Globalization;
using System.Text.Json;
using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Public;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Mappings;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class AgentCampaignWorkflowOrchestrationService : IAgentCampaignWorkflowOrchestrationService
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ITemplatedEmailService _emailService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly ILeadProposalConfidenceGateService _leadProposalConfidenceGateService;
    private readonly IProposalAccessTokenService _proposalAccessTokenService;
    private readonly ICampaignExecutionTaskService _campaignExecutionTaskService;
    private readonly ICampaignStatusTransitionService _campaignStatusTransitionService;
    private readonly IProspectDispositionService _prospectDispositionService;
    private readonly IRecommendationApprovalWorkflowService _recommendationApprovalWorkflowService;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly FormOptionsService _formOptionsService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AgentCampaignWorkflowOrchestrationService> _logger;

    public AgentCampaignWorkflowOrchestrationService(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IPackagePurchaseService packagePurchaseService,
        IEmailVerificationService emailVerificationService,
        ITemplatedEmailService emailService,
        IChangeAuditService changeAuditService,
        IRecommendationDocumentService recommendationDocumentService,
        ILeadProposalConfidenceGateService leadProposalConfidenceGateService,
        IProposalAccessTokenService proposalAccessTokenService,
        ICampaignExecutionTaskService campaignExecutionTaskService,
        ICampaignStatusTransitionService campaignStatusTransitionService,
        IProspectDispositionService prospectDispositionService,
        IRecommendationApprovalWorkflowService recommendationApprovalWorkflowService,
        IAgentCampaignOwnershipService ownershipService,
        FormOptionsService formOptionsService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AgentCampaignWorkflowOrchestrationService> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _packagePurchaseService = packagePurchaseService;
        _emailVerificationService = emailVerificationService;
        _emailService = emailService;
        _changeAuditService = changeAuditService;
        _recommendationDocumentService = recommendationDocumentService;
        _leadProposalConfidenceGateService = leadProposalConfidenceGateService;
        _proposalAccessTokenService = proposalAccessTokenService;
        _campaignExecutionTaskService = campaignExecutionTaskService;
        _campaignStatusTransitionService = campaignStatusTransitionService;
        _prospectDispositionService = prospectDispositionService;
        _recommendationApprovalWorkflowService = recommendationApprovalWorkflowService;
        _ownershipService = ownershipService;
        _formOptionsService = formOptionsService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<AgentCampaignActionResult> AssignAsync(Guid id, AssignCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;
        var agentUserId = request.AgentUserId ?? currentUserId;

        if (currentUser.Role == UserRole.Agent)
        {
            if (request.AgentUserId.HasValue && request.AgentUserId != currentUserId)
            {
                throw new ForbiddenException("Agents can only assign campaigns to themselves.");
            }

            agentUserId = currentUserId;
        }

        var campaign = currentUser.Role == UserRole.Agent
            ? await _ownershipService.GetOwnedOrClaimableCampaignAsync(id, currentUser, query => query, cancellationToken)
            : await _db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Campaign not found.");

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

    public async Task<AgentCampaignActionResult> UnassignAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = currentUser.Role == UserRole.Agent
            ? await _ownershipService.GetOwnedCampaignAsync(id, currentUser, query => query, cancellationToken)
            : await _db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new NotFoundException("Campaign not found.");

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

    public async Task<AgentCampaignActionResult> ConvertProspectToSaleAsync(Guid id, ConvertProspectToSaleRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.PackageOrder)
                .Include(x => x.PackageBand)
                .Include(x => x.User!)
                    .ThenInclude(x => x.BusinessProfile)
                .Include(x => x.ProspectLead)
                .Include(x => x.AssignedAgentUser)
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
                    .ThenInclude(x => x.RecommendationItems),
            cancellationToken);

        if (!ProspectCampaignPolicy.IsProspectiveCampaign(campaign))
        {
            throw new InvalidOperationException("Only prospective campaigns can be converted to a sale.");
        }

        if (string.Equals(campaign.PackageOrder.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(campaign.ToDetail(currentUser.Id));
        }

        var paymentReference = NormalizeOptionalText(request.PaymentReference)
            ?? $"agent-sale-{DateTime.UtcNow:yyyyMMddHHmmss}-{campaign.Id.ToString("N")[..8]}";

        await _packagePurchaseService.MarkOrderPaidAsync(campaign.PackageOrderId, paymentReference, cancellationToken);

        var refreshedCampaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (refreshedCampaign.User is not null && !refreshedCampaign.User.EmailVerified)
        {
            await _emailVerificationService.QueueActivationEmailAsync(refreshedCampaign.User, null, cancellationToken);
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

    public async Task<AgentCampaignActionResult> MarkLaunchedAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.User)
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder)
                .Include(x => x.CampaignRecommendations),
            cancellationToken);

        try
        {
            _campaignStatusTransitionService.MoveCampaignToLaunched(campaign, DateTime.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign is not ready to be marked live.",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _campaignExecutionTaskService.MarkTaskCompletedAsync(campaign.Id, "booking_confirmation", cancellationToken);
        await _campaignExecutionTaskService.MarkTaskCompletedAsync(campaign.Id, "tracking_links", cancellationToken);
        await _campaignExecutionTaskService.MarkTaskOpenAsync(campaign.Id, "first_report_snapshot", cancellationToken);
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

    public async Task<AgentCampaignActionResult> SendToClientAsync(Guid id, SendToClientRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.User)
                .Include(x => x.ProspectLead)
                .Include(x => x.CampaignRecommendations)
                    .ThenInclude(x => x.RecommendationItems)
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder),
            cancellationToken);

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }
        var recommendationsToSend = ResolveRecommendationsToSend(currentRecommendations);

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            throw new InvalidOperationException("Reopen this prospect before sending a recommendation to the client.");
        }

        var sendValidation = CampaignSendValidationSupport.Build(campaign, recommendationsToSend);
        if (!sendValidation.CanSendRecommendation)
        {
            return BadRequest(new
            {
                Message = sendValidation.Reasons.FirstOrDefault() ?? "This recommendation set is not ready to send.",
                Reasons = sendValidation.Reasons
            });
        }

        var useLeadTemplate = ShouldUseLeadOutreachMessage(campaign);
        if (useLeadTemplate)
        {
            try
            {
                await _leadProposalConfidenceGateService.EnsureCampaignReadyAsync(campaign.Id, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        try
        {
            foreach (var recommendation in recommendationsToSend)
            {
                await _recommendationDocumentService.GetRecommendationPdfBytesAsync(campaign.Id, recommendation.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recommendation PDF preflight failed for campaign {CampaignId}.", campaign.Id);
            return BadRequest(new { Message = "Recommendation PDFs are not ready to send yet." });
        }

        _campaignStatusTransitionService.MoveRecommendationSetToReviewReady(campaign, recommendationsToSend, DateTime.UtcNow);

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
                ProposalCount = recommendationsToSend.Count,
                request.Message
            },
            cancellationToken);

        await SendRecommendationReadyEmailIfNeededAsync(campaign.Id, recommendationsToSend, request.Message, currentUser, cancellationToken);

        return Accepted(new
        {
            CampaignId = id,
            ProposalCount = recommendationsToSend.Count,
            Message = "Recommendation set sent to client.",
            ClientMessage = request.Message
        });
    }

    public async Task<IReadOnlyList<FormOptionResponse>> GetProspectDispositionReasonsAsync(CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        return await _formOptionsService.GetOptionsAsync(FormOptionSetKeys.ProspectDispositionReasons, cancellationToken);
    }

    public async Task<AgentCampaignActionResult> RequestRecommendationChangesAsync(Guid id, RequestRecommendationChangesRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query.Include(x => x.CampaignRecommendations),
            cancellationToken);

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            throw new InvalidOperationException("Reopen this prospect before requesting recommendation changes.");
        }

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }

        if (!currentRecommendations.Any(x => string.Equals(x.Status, RecommendationStatuses.SentToClient, StringComparison.OrdinalIgnoreCase))
            && !string.Equals(campaign.Status, CampaignStatuses.ReviewReady, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Message = "Only recommendation sets that are awaiting client review can be returned for changes." });
        }

        var result = await _recommendationApprovalWorkflowService.RequestChangesAsync(id, request.Notes, cancellationToken);
        await WriteChangeAuditAsync(
            "agent_request_recommendation_changes",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Agent captured recommendation changes for {ResolveCampaignLabel(campaign)} and reopened the recommendation set as a draft revision.",
            new
            {
                CampaignId = campaign.Id,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            },
            cancellationToken);

        return Accepted(new
        {
            result.CampaignId,
            result.RecommendationId,
            result.Status,
            result.Message
        });
    }

    public async Task<AgentCampaignActionResult> CloseProspectAsync(Guid id, CloseProspectCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder)
                .Include(x => x.AssignedAgentUser)
                .Include(x => x.ProspectDispositionClosedByUser),
            cancellationToken);

        await _prospectDispositionService.CloseAsync(campaign, currentUser.Id, currentUser.Role, request.ReasonCode, request.Notes, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "close_prospect",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Closed prospect {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                request.ReasonCode,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            },
            cancellationToken);

        _db.ChangeTracker.Clear();
        var refreshedCampaign = await LoadCampaignDetailAsync(campaign.Id, cancellationToken);
        var response = refreshedCampaign.ToDetail(currentUser.Id);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUser.Id);
        return Ok(response);
    }

    public async Task<AgentCampaignActionResult> ReopenProspectAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder)
                .Include(x => x.AssignedAgentUser)
                .Include(x => x.ProspectDispositionClosedByUser),
            cancellationToken);

        await _prospectDispositionService.ReopenAsync(campaign, currentUser.Id, currentUser.Role, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "reopen_prospect",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Reopened prospect {ResolveCampaignLabel(campaign)}.",
            new { CampaignId = campaign.Id },
            cancellationToken);

        _db.ChangeTracker.Clear();
        var refreshedCampaign = await LoadCampaignDetailAsync(campaign.Id, cancellationToken);
        var response = refreshedCampaign.ToDetail(currentUser.Id);
        var queueStage = CampaignWorkflowPolicy.ResolveAgentQueueStage(refreshedCampaign);
        response.NextAction = CampaignWorkflowPolicy.GetAgentNextAction(refreshedCampaign, queueStage, currentUser.Id);
        return Ok(response);
    }

    public async Task<AgentCampaignActionResult> ResendProposalEmailAsync(Guid id, string toEmail, string? message, CancellationToken cancellationToken)
    {
        var senderUser = await GetCurrentOperationsUserAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return BadRequest(new { Message = "Recipient email is required." });
        }

        toEmail = toEmail.Trim();
        if (!IsValidEmailAddress(toEmail))
        {
            return BadRequest(new { Message = "Recipient email is not a valid address." });
        }

        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            senderUser,
            query => query
                .Include(x => x.User)
                .Include(x => x.ProspectLead)
                .Include(x => x.CampaignBrief)
                .Include(x => x.AssignedAgentUser)
                .Include(x => x.CampaignRecommendations)
                    .ThenInclude(x => x.RecommendationItems)
                .Include(x => x.PackageBand)
                .Include(x => x.PackageOrder),
            cancellationToken);

        if (ProspectCampaignPolicy.IsClosed(campaign))
        {
            return BadRequest(new { Message = "Reopen this prospect before resending proposal emails." });
        }

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Length == 0)
        {
            return BadRequest(new { Message = "No current recommendations found for this campaign." });
        }
        var recommendationsToSend = ResolveRecommendationsToSend(currentRecommendations);

        try
        {
            await SendRecommendationReadyEmailAsync(campaign, recommendationsToSend, message, senderUser, toEmail, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend proposal email failed for campaign {CampaignId} to {ToEmail}.", campaign.Id, toEmail);
            return new AgentCampaignActionResult(
                StatusCodes.Status502BadGateway,
                new
                {
                    Message = "Email send failed. Check Resend configuration/suppressions and try again.",
                    Detail = ex.Message
                });
        }

        await WriteChangeAuditAsync(
            "resend_proposal_email",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Resent proposal email to {toEmail}.",
            new
            {
                CampaignId = campaign.Id,
                ProposalCount = recommendationsToSend.Count,
                ToEmail = toEmail,
                Message = message
            },
            cancellationToken);

        return Accepted(new { CampaignId = id, ToEmail = toEmail, ProposalCount = recommendationsToSend.Count, Message = "Proposal email resent." });
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

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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

    private string BuildPublicProposalPdfUrl(Guid campaignId)
    {
        var accessToken = _proposalAccessTokenService.CreateToken(campaignId);
        return BuildFrontendUrl($"/api/public/proposals/{campaignId:D}/recommendation-pdf?token={Uri.EscapeDataString(accessToken)}");
    }

    private string BuildLeadProposalUrl(Guid campaignId)
    {
        var accessToken = _proposalAccessTokenService.CreateToken(campaignId);
        return BuildFrontendUrl($"/proposal/{campaignId:D}?token={Uri.EscapeDataString(accessToken)}");
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
        }
    }

    private async Task SendCampaignLaunchedEmailAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "campaign-live",
                campaign.ResolveClientEmail(),
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.ResolveClientName(),
                    ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["CampaignUrl"] = BuildClientCampaignUrl(campaign)
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send campaign launched email for campaign {CampaignId}.", campaign.Id);
        }
    }

    private async Task SendRecommendationReadyEmailIfNeededAsync(
        Guid campaignId,
        IReadOnlyList<CampaignRecommendation> recommendations,
        string? agentMessage,
        UserAccount senderUser,
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
            .Include(x => x.CampaignBrief)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.EmailDeliveryMessages)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null || EmailDeliveryPurposePolicy.HasTrackedDelivery(campaign.EmailDeliveryMessages, "recommendation_ready"))
        {
            return;
        }

        await SendRecommendationReadyEmailAsync(campaign, recommendations, agentMessage, senderUser, campaign.ResolveClientEmail(), cancellationToken);
    }

    private async Task SendRecommendationReadyEmailAsync(
        Campaign campaign,
        IReadOnlyList<CampaignRecommendation> recommendations,
        string? agentMessage,
        UserAccount senderUser,
        string toEmail,
        CancellationToken cancellationToken)
    {
        var useLeadTemplate = ShouldUseLeadOutreachMessage(campaign);
        EmailAttachment[]? attachments = null;
        var proposalCount = recommendations.Count;
        var templateName = useLeadTemplate
            ? "lead-proposal-ready"
            : "recommendation-ready";
        var recommendationEmailContext = ResolveRecommendationEmailContext(recommendations);

        try
        {
            var recommendationPdfs = new List<EmailAttachment>(recommendations.Count);
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
            if (attachments.Length != recommendations.Count)
            {
                throw new InvalidOperationException($"Expected {recommendations.Count} recommendation PDF attachments but built {attachments.Length}.");
            }
            _logger.LogInformation(
                "Built {AttachmentCount} recommendation PDF attachments for campaign {CampaignId} using template {TemplateName}.",
                attachments.Length,
                campaign.Id,
                templateName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build recommendation PDF attachment for campaign {CampaignId}.", campaign.Id);
            throw new InvalidOperationException("Recommendation PDFs could not be generated for the email send.", ex);
        }

        var resolvedAgentMessage = useLeadTemplate
            ? null
            : ResolveRecommendationReadyAgentMessage(campaign, senderUser, agentMessage);
        var recommendationIntro = ResolveRecommendationReadyIntro(campaign, senderUser);
        var areaOrIndustry = ResolveLeadAreaOrIndustry(campaign);
        var proposalActionButtons = useLeadTemplate
            ? string.Empty
            : BuildProposalAcceptButtonsBlock(campaign.Id, recommendations);
        var leadPdfUrl = useLeadTemplate ? BuildPublicProposalPdfUrl(campaign.Id) : null;
        var reviewUrl = useLeadTemplate ? BuildLeadProposalUrl(campaign.Id) : BuildProposalUrl(campaign.Id);

        await _emailService.SendAsync(
            templateName,
            toEmail,
            "noreply",
            new Dictionary<string, string?>
            {
                ["AgentName"] = ResolveAgentDisplayName(campaign, senderUser),
                ["ClientName"] = campaign.ResolveClientName(),
                ["CampaignName"] = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                ["PackageName"] = campaign.PackageBand.Name,
                ["BudgetLabel"] = ResolveBudgetLabel(campaign),
                ["Budget"] = ResolveBudgetDisplayText(campaign),
                ["RecommendationEmailSubject"] = recommendationEmailContext.Subject,
                ["RecommendationEmailHeadline"] = recommendationEmailContext.Headline,
                ["RecommendationIntro"] = recommendationIntro,
                ["AreaOrIndustry"] = areaOrIndustry,
                ["ReviewUrl"] = reviewUrl,
                ["LeadPdfUrl"] = leadPdfUrl,
                ["ProposalCount"] = proposalCount.ToString(CultureInfo.InvariantCulture),
                ["ProposalSummary"] = recommendationEmailContext.Summary,
                ["AgentMessageBlock"] = BuildAgentMessageBlock(resolvedAgentMessage),
                ["RecommendationPackBlock"] = recommendationEmailContext.PackBlock,
                ["ProposalAcceptButtonsBlock"] = proposalActionButtons
            },
            attachments,
            new EmailTrackingContext
            {
                Purpose = "recommendation_ready",
                CampaignId = campaign.Id,
                RecommendationRevisionNumber = recommendations[0].RevisionNumber,
                RecipientUserId = campaign.UserId,
                ProspectLeadId = campaign.ProspectLeadId,
                Metadata = new Dictionary<string, string?>
                {
                    ["proposalCount"] = proposalCount.ToString(CultureInfo.InvariantCulture),
                    ["templateName"] = templateName,
                    ["agentMessage"] = resolvedAgentMessage
                }
            },
            cancellationToken);

        _logger.LogInformation("Proposal email sent for campaign {CampaignId} to {ToEmail} using template {TemplateName}.", campaign.Id, toEmail, templateName);
    }

    private static RecommendationEmailContext ResolveRecommendationEmailContext(IReadOnlyList<CampaignRecommendation> recommendations)
    {
        var selectedProposalLabel = recommendations
            .Select(item => ExtractSelectedProposalLabel(ExtractClientFeedbackNotes(item.Rationale)))
            .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label));
        var isRevision = recommendations.Any(item => !string.IsNullOrWhiteSpace(ExtractClientFeedbackNotes(item.Rationale)));

        if (!isRevision)
        {
            return new RecommendationEmailContext(
                "Your Advertified recommendation options are ready for {{CampaignName}}",
                "Your campaign recommendations are ready to review",
                recommendations.Count > 1
                    ? $"We have prepared {recommendations.Count} proposal options for you to compare."
                    : "We have prepared your recommendation for review.",
                @"
                <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                  We have attached a separate PDF for each recommendation option. Each PDF starts with a one-page summary followed by the full detailed media plan.
                </p>");
        }

        var proposalReference = string.IsNullOrWhiteSpace(selectedProposalLabel)
            ? "your selected proposal"
            : selectedProposalLabel;

        return new RecommendationEmailContext(
            string.IsNullOrWhiteSpace(selectedProposalLabel)
                ? "Your revised Advertified recommendation is ready for {{CampaignName}}"
                : $"Your revised {selectedProposalLabel} is ready for {{CampaignName}}",
            string.IsNullOrWhiteSpace(selectedProposalLabel)
                ? "Your revised recommendation is ready to review"
                : $"Your revised {selectedProposalLabel} is ready to review",
            recommendations.Count > 1
                ? $"We have updated {proposalReference} based on your feedback. The revised proposal is attached, and your review page includes the latest full proposal set if you want to compare options again."
                : $"We have updated {proposalReference} based on your feedback and attached the revised proposal for review.",
            recommendations.Count > 1
                ? @"
                <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                  We have attached the updated proposal PDFs for this revision. Your selected proposal has been revised, and the latest full proposal set is still available in your review workspace if you want to compare routes again.
                </p>"
                : @"
                <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                  We have attached the updated proposal PDF for this revision so you can review the changes directly.
                </p>");
    }

    private static IReadOnlyList<CampaignRecommendation> ResolveRecommendationsToSend(IReadOnlyList<CampaignRecommendation> recommendations)
    {
        var selectedProposalLabel = recommendations
            .Select(item => ExtractSelectedProposalLabel(ExtractClientFeedbackNotes(item.Rationale)))
            .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label));
        if (string.IsNullOrWhiteSpace(selectedProposalLabel))
        {
            return recommendations;
        }

        var selectedRecommendation = recommendations.FirstOrDefault(item =>
            string.Equals(item.RecommendationType, selectedProposalLabel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(GetProposalLabel(item), selectedProposalLabel, StringComparison.OrdinalIgnoreCase));
        if (selectedRecommendation is not null)
        {
            return new[] { selectedRecommendation };
        }

        return recommendations;
    }

    private static string ResolveAgentDisplayName(Campaign campaign, UserAccount senderUser)
    {
        if (!string.IsNullOrWhiteSpace(campaign.AssignedAgentUser?.FullName))
        {
            return campaign.AssignedAgentUser.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(senderUser.FullName))
        {
            return senderUser.FullName.Trim();
        }

        return "the Advertified team";
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

    private static string GetProposalLabel(CampaignRecommendation recommendation)
    {
        var variantKey = recommendation.RecommendationType?
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()?
            .ToLowerInvariant();

        return variantKey switch
        {
            "balanced" => "Proposal A",
            "ooh_focus" => "Proposal B",
            "radio_focus" => "Proposal C",
            "digital_focus" => "Proposal C",
            _ => string.Empty
        };
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

    private static string? ExtractSelectedProposalLabel(string? clientFeedbackNotes)
    {
        if (string.IsNullOrWhiteSpace(clientFeedbackNotes))
        {
            return null;
        }

        var line = clientFeedbackNotes
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(entry => entry.StartsWith("Client selected proposal for revision:", StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return null;
        }

        var proposalLabel = line["Client selected proposal for revision:".Length..].Trim();
        return string.IsNullOrWhiteSpace(proposalLabel) ? null : proposalLabel;
    }

    private static string ResolveBudgetLabel(Campaign campaign)
    {
        return ShouldDisplayPackageRange(campaign)
            ? "Package range"
            : "Selected budget";
    }

    private static string ResolveBudgetDisplayText(Campaign campaign)
    {
        if (ShouldDisplayPackageRange(campaign))
        {
            return $"{FormatCurrency(campaign.PackageBand.MinBudget)} to {FormatCurrency(campaign.PackageBand.MaxBudget)}";
        }

        return FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount);
    }

    private static bool ShouldDisplayPackageRange(Campaign campaign)
    {
        return campaign.PackageOrder.SelectedBudget is null or 0m;
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("C0", CultureInfo.GetCultureInfo("en-ZA"));
    }

    private sealed record RecommendationEmailContext(string Subject, string Headline, string Summary, string PackBlock);

    private static string BuildAgentMessageBlock(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var escapedMessage = System.Net.WebUtility.HtmlEncode(message.Trim())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br/>", StringComparison.Ordinal);
        return $@"
                    <div style=""background-color:#f5f5f5;padding:16px;border-left:4px solid #4b635a;margin:16px 0;"">
                      <p style=""margin:0;font-size:14px;font-style:italic;color:#666;"">
                        <strong>Message from your Agent:</strong><br/>
                        {escapedMessage}
                      </p>
                    </div>";
    }

    private static string? ResolveRecommendationReadyAgentMessage(Campaign campaign, UserAccount senderUser, string? agentMessage)
    {
        if (!string.IsNullOrWhiteSpace(agentMessage))
        {
            return agentMessage.Trim();
        }

        if (!ShouldUseLeadOutreachMessage(campaign))
        {
            return null;
        }

        var senderName = !string.IsNullOrWhiteSpace(senderUser.FullName)
            ? senderUser.FullName.Trim()
            : (!string.IsNullOrWhiteSpace(campaign.AssignedAgentUser?.FullName)
                ? campaign.AssignedAgentUser!.FullName.Trim()
                : "your Advertified strategist");

        var businessName = !string.IsNullOrWhiteSpace(campaign.ResolveBusinessName())
            ? campaign.ResolveBusinessName()
            : campaign.ResolveClientName();
        var areaOrIndustry = ResolveLeadAreaOrIndustry(campaign);

        return $@"I'm {senderName} from Advertified â€” we help businesses find where they're losing customers online and put campaigns in place to fix it.
We recently looked at {businessName}'s market presence and identified a specific gap in how your business is capturing demand in {areaOrIndustry}. It's fixable, and we've already mapped out three campaign approaches tailored to your situation.

Here's what makes this easy to act on:
- The campaigns are ready to review â€” no lengthy onboarding
- Each option is built around your budget, not a fixed package
- We offer Buy Now, Pay Later â€” you can start generating results before you've paid in full

Most businesses hold back on marketing spend because of cash flow. Our BNPL structure means you're not paying upfront â€” you're paying from growth.
If useful, we can walk you through it in a quick 15-minute call â€” no commitment, just clarity.";
    }

    private static string ResolveRecommendationReadyIntro(Campaign campaign, UserAccount senderUser)
    {
        if (!ShouldUseLeadOutreachMessage(campaign))
        {
            return $"Hi {campaign.ResolveClientName()}, your Advertified strategist has prepared recommendation options for {ResolveCampaignLabel(campaign)}.";
        }

        var senderName = !string.IsNullOrWhiteSpace(senderUser.FullName)
            ? senderUser.FullName.Trim()
            : (!string.IsNullOrWhiteSpace(campaign.AssignedAgentUser?.FullName)
                ? campaign.AssignedAgentUser!.FullName.Trim()
                : "your Advertified strategist");

        return $"Good Day, I'm {senderName} from Advertified.";
    }

    private static string ResolveLeadAreaOrIndustry(Campaign campaign)
    {
        var cities = DeserializeStringList(campaign.CampaignBrief?.CitiesJson);
        if (cities.Length > 0)
        {
            return string.Join(", ", cities.Take(2));
        }

        var provinces = DeserializeStringList(campaign.CampaignBrief?.ProvincesJson);
        if (provinces.Length > 0)
        {
            return string.Join(", ", provinces.Take(2));
        }

        if (!string.IsNullOrWhiteSpace(campaign.CampaignBrief?.Objective))
        {
            return campaign.CampaignBrief.Objective.Trim();
        }

        return "your market";
    }

    private static string[] DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool ShouldUseLeadOutreachMessage(Campaign campaign)
    {
        return ProspectCampaignPolicy.IsProspectiveCampaign(campaign)
            || LeadOutreachCampaignSupport.IsLeadOutreachCampaign(campaign);
    }

    private string BuildProposalAcceptButtonsBlock(Guid campaignId, IReadOnlyList<CampaignRecommendation> recommendations)
    {
        if (recommendations.Count == 0)
        {
            return string.Empty;
        }

        var buttons = string.Join("&nbsp;&nbsp;",
            recommendations.Select((r, index) => $@"
                <a href=""{BuildProposalUrl(campaignId, r.Id, "accept")}"" 
                   style=""display:inline-block;padding:10px 20px;background-color:#4b635a;color:white;text-decoration:none;border-radius:4px;font-size:14px;"">
                  Accept Proposal {(index + 1)}
                </a>"));

        return $@"
                    <div style=""text-align:center;margin:24px 0;"">
                      {buttons}
                    </div>";
    }

    private static bool IsValidEmailAddress(string value)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AgentCampaignActionResult Accepted(object payload) => new(StatusCodes.Status202Accepted, payload);

    private static AgentCampaignActionResult Ok(object payload) => new(StatusCodes.Status200OK, payload);

    private static AgentCampaignActionResult BadRequest(object payload) => new(StatusCodes.Status400BadRequest, payload);
}

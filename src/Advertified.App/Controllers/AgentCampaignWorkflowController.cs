using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Campaigns;
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
using System.Globalization;

namespace Advertified.App.Controllers;

/// <summary>
/// Handles campaign workflow operations: assignment, conversion, state transitions, and client communication.
/// This controller manages operations that change campaign status and routing.
/// </summary>
[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentCampaignWorkflowController : ControllerBase
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private const string ManualReviewMarker = "Manual review required:";
    
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPackagePurchaseService _packagePurchaseService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ITemplatedEmailService _emailService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly IRecommendationDocumentService _recommendationDocumentService;
    private readonly IProposalAccessTokenService _proposalAccessTokenService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AgentCampaignWorkflowController> _logger;

    public AgentCampaignWorkflowController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IPackagePurchaseService packagePurchaseService,
        IEmailVerificationService emailVerificationService,
        ITemplatedEmailService emailService,
        IChangeAuditService changeAuditService,
        IRecommendationDocumentService recommendationDocumentService,
        IProposalAccessTokenService proposalAccessTokenService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AgentCampaignWorkflowController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _packagePurchaseService = packagePurchaseService;
        _emailVerificationService = emailVerificationService;
        _emailService = emailService;
        _changeAuditService = changeAuditService;
        _recommendationDocumentService = recommendationDocumentService;
        _proposalAccessTokenService = proposalAccessTokenService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
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

    [HttpPost("{id:guid}/convert-to-sale")]
    public async Task<IActionResult> ConvertProspectToSale(Guid id, [FromBody] ConvertProspectToSaleRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.PackageBand)
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.ProspectLead)
            .Include(x => x.AssignedAgentUser)
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

        if (currentUser.Role == UserRole.Agent && campaign.AssignedAgentUserId != currentUser.Id)
        {
            throw new InvalidOperationException("Only the assigned agent can convert this campaign to a sale.");
        }

        if (!string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase))
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

    [HttpPost("{id:guid}/mark-launched")]
    public async Task<IActionResult> MarkLaunched(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignRecommendations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (campaign.Status is CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched
            || campaign.CampaignRecommendations.Any(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { Message = "This campaign has already been approved and can no longer be regenerated from the recommendation workspace." });
        }

        if (!string.Equals(campaign.Status, CampaignStatuses.CreativeApproved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(campaign.Status, CampaignStatuses.BookingInProgress, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign is not ready to be marked live.",
                Detail = "Only campaigns with final creative approval captured or supplier booking underway can be activated as live.",
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

    [HttpPost("{id:guid}/send-to-client")]
    public async Task<IActionResult> SendToClient(Guid id, [FromBody] SendToClientRequest request, CancellationToken cancellationToken)
    {
        await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
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

        if (campaign.Status is CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched
            || currentRecommendations.Any(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { Message = "This campaign is already approved and can no longer be sent back to the client from the recommendation stage." });
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

    private async Task SendAssignmentEmailIfNeededAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
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

    private async Task SendCampaignLaunchedEmailAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "campaign-launched",
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
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.ProspectLead)
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
                campaign.ResolveClientEmail(),
                "noreply",
                new Dictionary<string, string?>
                {
                    ["ClientName"] = campaign.ResolveClientName(),
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

    private static string BuildAgentMessageBlock(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var escapedMessage = System.Net.WebUtility.HtmlEncode(message.Trim());
        return $@"
                    <div style=""background-color:#f5f5f5;padding:16px;border-left:4px solid #4b635a;margin:16px 0;"">
                      <p style=""margin:0;font-size:14px;font-style:italic;color:#666;"">
                        <strong>Message from your Agent:</strong><br/>
                        {escapedMessage}
                      </p>
                    </div>";
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

    private static bool IsProspectiveCampaign(Campaign campaign)
    {
        return string.Equals(campaign.Status, CampaignStatuses.AwaitingPurchase, StringComparison.OrdinalIgnoreCase)
            || (campaign.PackageOrder?.PaymentProvider == "prospect" && 
                !string.Equals(campaign.PackageOrder?.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase));
    }
}

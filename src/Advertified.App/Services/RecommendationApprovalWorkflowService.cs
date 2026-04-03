using System.Globalization;
using Advertified.App.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class RecommendationApprovalWorkflowService : IRecommendationApprovalWorkflowService
{
    private readonly AppDbContext _db;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<RecommendationApprovalWorkflowService> _logger;

    public RecommendationApprovalWorkflowService(
        AppDbContext db,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<RecommendationApprovalWorkflowService> logger)
    {
        _db = db;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<RecommendationWorkflowResult> ApproveAsync(Guid campaignId, Guid? recommendationId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.CampaignRecommendations)
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.AssignedAgentUser)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        var recommendation = currentRecommendations
            .FirstOrDefault(x => x.Id == recommendationId)
            ?? currentRecommendations.FirstOrDefault()
            ?? throw new InvalidOperationException("Recommendation not found.");

        if (!CampaignOperationsPolicy.IsOrderOperationallyActive(campaign.PackageOrder))
        {
            throw new InvalidOperationException("Payment required before approval.");
        }

        var now = DateTime.UtcNow;
        recommendation.Status = RecommendationStatuses.Approved;
        recommendation.ApprovedAt = now;
        recommendation.UpdatedAt = now;
        campaign.Status = CampaignStatuses.Approved;
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        await SendRecommendationApprovedEmailAsync(campaign, cancellationToken);
        await SendActivationInProgressEmailAsync(campaign, cancellationToken);
        await SendInternalCreativeQueueUpdateAsync(
            campaign,
            eventTitle: "Creative production can now begin",
            eventBody: "Recommendation approved by client. Move into studio production and prepare the creative system.",
            actionPath: $"/creative/campaigns/{campaign.Id}/studio",
            includeAssignedAgent: true,
            cancellationToken);

        return new RecommendationWorkflowResult
        {
            CampaignId = campaign.Id,
            RecommendationId = recommendation.Id,
            Status = recommendation.Status,
            Message = "Recommendation approved."
        };
    }

    public async Task<RecommendationWorkflowResult> RequestChangesAsync(Guid campaignId, string? notes, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Length == 0)
        {
            throw new InvalidOperationException("Recommendation not found.");
        }

        var now = DateTime.UtcNow;
        var nextRevisionNumber = RecommendationRevisionSupport.GetNextRevisionNumber(campaign.CampaignRecommendations);
        var clonedRecommendations = RecommendationRevisionSupport.CloneAsDraftRevision(currentRecommendations, nextRevisionNumber, now, notes);
        _db.CampaignRecommendations.AddRange(clonedRecommendations);
        campaign.Status = CampaignStatuses.PlanningInProgress;
        campaign.RecommendationReadyEmailSentAt = null;
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return new RecommendationWorkflowResult
        {
            CampaignId = campaign.Id,
            RecommendationId = clonedRecommendations[0].Id,
            Status = campaign.Status,
            Message = "Recommendation returned for changes."
        };
    }

    private async Task SendRecommendationApprovedEmailAsync(Campaign campaign, CancellationToken cancellationToken)
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
                    ["CampaignName"] = ResolveCampaignName(campaign),
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

    private async Task SendActivationInProgressEmailAsync(Campaign campaign, CancellationToken cancellationToken)
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
                    ["CampaignName"] = ResolveCampaignName(campaign),
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

            var actionUrl = BuildFrontendUrl(actionPath);
            foreach (var email in recipientEmails)
            {
                await _emailService.SendAsync(
                    "creative-queue-update",
                    email,
                    "campaigns",
                    new Dictionary<string, string?>
                    {
                        ["CampaignName"] = ResolveCampaignName(campaign),
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

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string ResolveCampaignName(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand.Name} campaign"
            : campaign.CampaignName.Trim();
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }
}

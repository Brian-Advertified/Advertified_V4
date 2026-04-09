using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Advertified.App.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/prospect-questionnaires")]
[AllowAnonymous]
[EnableRateLimiting("public_general")]
public sealed class PublicProspectQuestionnaireController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SaveCampaignBriefRequestValidator _briefValidator;
    private readonly IAgentAreaRoutingService _agentAreaRoutingService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<PublicProspectQuestionnaireController> _logger;

    public PublicProspectQuestionnaireController(
        AppDbContext db,
        SaveCampaignBriefRequestValidator briefValidator,
        IAgentAreaRoutingService agentAreaRoutingService,
        IChangeAuditService changeAuditService,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<PublicProspectQuestionnaireController> logger)
    {
        _db = db;
        _briefValidator = briefValidator;
        _agentAreaRoutingService = agentAreaRoutingService;
        _changeAuditService = changeAuditService;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<PublicProspectQuestionnaireResponse>> Submit(
        [FromBody] PublicProspectQuestionnaireRequest request,
        CancellationToken cancellationToken)
    {
        await _briefValidator.ValidateAndThrowAsync(request.Brief, cancellationToken);

        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = (request.Email?.Trim() ?? string.Empty).ToLowerInvariant();
        var phone = request.Phone?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new InvalidOperationException("Phone is required.");
        }

        var packageBand = await _db.PackageBands
            .FirstOrDefaultAsync(x => x.Id == request.PackageBandId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Package band not found.");

        var selectedBudget = ResolveProspectBudget(packageBand);
        var lead = await _db.ProspectLeads
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        var createdNewLead = false;
        if (lead is null)
        {
            lead = new ProspectLead
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Phone = phone,
                Source = "public_questionnaire",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ProspectLeads.Add(lead);
            createdNewLead = true;
        }
        else
        {
            lead.FullName = fullName;
            lead.Phone = phone;
            lead.UpdatedAt = DateTime.UtcNow;
        }

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
            ? $"{packageBand.Name} prospect questionnaire"
            : request.CampaignName.Trim();
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            ProspectLeadId = lead.Id,
            PackageOrderId = packageOrder.Id,
            PackageBandId = packageBand.Id,
            CampaignName = campaignName,
            Status = CampaignStatuses.BriefSubmitted,
            PlanningMode = "hybrid",
            AiUnlocked = true,
            AgentAssistanceRequested = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Campaigns.Add(campaign);

        var brief = new CampaignBrief
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            CreatedAt = now
        };
        CampaignBriefMapper.Apply(brief, request.Brief, now);
        brief.SubmittedAt = now;
        _db.CampaignBriefs.Add(brief);
        _db.CampaignBriefDrafts.Add(new CampaignBriefDraft
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            DraftJson = JsonSerializer.Serialize(request.Brief),
            SavedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _agentAreaRoutingService.TryAssignCampaignAsync(campaign.Id, "public_prospect_questionnaire_submitted", cancellationToken);
        await _changeAuditService.WriteAsync(
            null,
            "public",
            "submit_prospect_questionnaire",
            "campaign",
            campaign.Id.ToString(),
            campaignName,
            $"Prospect questionnaire submitted for {campaignName}.",
            new
            {
                CampaignId = campaign.Id,
                ProspectEmail = email,
                PackageBand = packageBand.Name,
                SelectedBudget = selectedBudget,
                createdNewLead
            },
            cancellationToken);

        try
        {
            await _emailService.SendAsync(
                "prospect-questionnaire-thank-you",
                email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["FirstName"] = ExtractFirstName(fullName),
                    ["CampaignName"] = campaignName,
                    ["PackageName"] = packageBand.Name,
                    ["SignInUrl"] = BuildFrontendUrl("/login")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send prospect questionnaire thank-you email for campaign {CampaignId}.", campaign.Id);
        }

        return Ok(new PublicProspectQuestionnaireResponse
        {
            CampaignId = campaign.Id,
            CampaignName = campaignName,
            Message = "Your campaign requirements have been submitted. An Advertified agent can now review them and prepare recommendations."
        });
    }

    private static decimal ResolveProspectBudget(PackageBand packageBand)
    {
        // Use the package floor until the client or agent confirms an exact spend.
        return packageBand.MinBudget > 0m ? packageBand.MinBudget : 25000m;
    }

    private string BuildFrontendUrl(string path)
    {
        var baseUrl = (_frontendOptions.BaseUrl ?? string.Empty).TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{baseUrl}{normalizedPath}";
    }

    private static string ExtractFirstName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "there";
        }

        return fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }
}

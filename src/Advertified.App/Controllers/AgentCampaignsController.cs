using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/campaigns")]
public sealed class AgentCampaignsController : ControllerBase
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICampaignBriefService _campaignBriefService;
    private readonly ICampaignRecommendationService _campaignRecommendationService;

    public AgentCampaignsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ICampaignBriefService campaignBriefService,
        ICampaignRecommendationService campaignRecommendationService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _campaignBriefService = campaignBriefService;
        _campaignRecommendationService = campaignRecommendationService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return Ok(campaigns.Select(x => x.ToListItem(currentUserId)).ToArray());
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.User)
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
            var stage = ResolveQueueStage(campaign);
            return new AgentInboxItemResponse
            {
                Id = campaign.Id,
                UserId = campaign.UserId,
                CampaignName = string.IsNullOrWhiteSpace(campaign.CampaignName) ? $"{campaign.PackageBand.Name} campaign" : campaign.CampaignName.Trim(),
                ClientName = campaign.User.FullName,
                ClientEmail = campaign.User.Email,
                PackageBandName = campaign.PackageBand.Name,
                SelectedBudget = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                Status = campaign.Status,
                PlanningMode = campaign.PlanningMode,
                QueueStage = stage,
                QueueLabel = GetQueueLabel(stage),
                AssignedAgentUserId = campaign.AssignedAgentUserId,
                AssignedAgentName = campaign.AssignedAgentUser?.FullName,
                AssignedAt = campaign.AssignedAt.HasValue ? new DateTimeOffset(campaign.AssignedAt.Value, TimeSpan.Zero) : null,
                IsAssignedToCurrentUser = campaign.AssignedAgentUserId == currentUserId,
                IsUnassigned = campaign.AssignedAgentUserId is null,
                NextAction = GetAgentNextAction(campaign, stage, currentUserId),
                HasBrief = campaign.CampaignBrief is not null,
                HasRecommendation = campaign.CampaignRecommendations.Any(),
                CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(campaign.UpdatedAt, TimeSpan.Zero)
            };
        })
        .OrderByDescending(x => x.IsAssignedToCurrentUser)
        .ThenByDescending(x => x.IsUnassigned)
        .ThenBy(x => GetQueueRank(x.QueueStage))
        .ThenByDescending(x => x.UpdatedAt)
        .ToArray();

        var response = new AgentInboxResponse
        {
            TotalCampaigns = items.Length,
            AssignedToMeCount = items.Count(x => x.IsAssignedToCurrentUser),
            UnassignedCount = items.Count(x => x.IsUnassigned),
            NewlyPaidCount = items.Count(x => x.QueueStage == "newly_paid"),
            BriefWaitingCount = items.Count(x => x.QueueStage == "brief_waiting"),
            PlanningReadyCount = items.Count(x => x.QueueStage == "planning_ready"),
            AgentReviewCount = items.Count(x => x.QueueStage == "agent_review"),
            ReadyToSendCount = items.Count(x => x.QueueStage == "ready_to_send"),
            WaitingOnClientCount = items.Count(x => x.QueueStage == "waiting_on_client"),
            CompletedCount = items.Count(x => x.QueueStage == "completed"),
            Items = items
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = campaign.ToDetail(currentUserId);
        var queueStage = ResolveQueueStage(campaign);
        response.NextAction = GetAgentNextAction(campaign, queueStage, currentUserId);

        if (string.IsNullOrWhiteSpace(response.CampaignName))
        {
            response.CampaignName = $"{campaign.PackageBand.Name} campaign";
        }

        return Ok(response);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignCampaignRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var agentUserId = request.AgentUserId ?? currentUserId;

        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        campaign.AssignedAgentUserId = agentUserId;
        campaign.AssignedAt = DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Accepted(new { CampaignId = id, AssignedAgentUserId = agentUserId, Message = "Campaign assigned." });
    }

    [HttpPost("{id:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        campaign.AssignedAgentUserId = null;
        campaign.AssignedAt = null;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Accepted(new { CampaignId = id, Message = "Campaign unassigned." });
    }

    [HttpPost("{id:guid}/recommendations")]
    public async Task<IActionResult> CreateRecommendation(Guid id, [FromBody] AgentRecommendationRequest request, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var now = DateTime.UtcNow;
        var recommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (recommendation is null)
        {
            recommendation = new CampaignRecommendation
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                RecommendationType = "agent_assisted",
                GeneratedBy = "agent",
                Status = "draft",
                TotalCost = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                Summary = request.Notes,
                Rationale = request.Notes,
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

        SyncRecommendationItems(recommendation, request.InventoryItems, now);
        recommendation.TotalCost = recommendation.RecommendationItems.Sum(x => x.TotalCost);
        if (recommendation.TotalCost <= 0)
        {
            recommendation.TotalCost = campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount;
        }

        campaign.Status = "planning_in_progress";
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return Accepted(new { CampaignId = id, RecommendationId = recommendation.Id, Message = "Recommendation saved." });
    }

    [HttpPost("{id:guid}/initialize-recommendation")]
    public async Task<IActionResult> InitializeRecommendation(Guid id, [FromBody] InitializeRecommendationFlowRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        await _campaignBriefService.SaveDraftAsync(campaign.UserId, id, request.Brief, cancellationToken);

        if (request.SubmitBrief)
        {
            await _campaignBriefService.SubmitAsync(campaign.UserId, id, cancellationToken);
            await _campaignBriefService.SetPlanningModeAsync(campaign.UserId, id, request.PlanningMode, cancellationToken);
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
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = ResolveQueueStage(refreshedCampaign);
        response.NextAction = GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        if (string.IsNullOrWhiteSpace(response.CampaignName))
        {
            response.CampaignName = $"{refreshedCampaign.PackageBand.Name} campaign";
        }

        return Ok(response);
    }

    [HttpPost("{id:guid}/generate-recommendation")]
    public async Task<IActionResult> GenerateRecommendation(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        campaign.AssignedAgentUserId ??= currentUserId;
        campaign.AssignedAt ??= DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _campaignRecommendationService.GenerateAndSaveAsync(id, cancellationToken);

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
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var response = refreshedCampaign.ToDetail(currentUserId);
        var queueStage = ResolveQueueStage(refreshedCampaign);
        response.NextAction = GetAgentNextAction(refreshedCampaign, queueStage, currentUserId);
        if (string.IsNullOrWhiteSpace(response.CampaignName))
        {
            response.CampaignName = $"{refreshedCampaign.PackageBand.Name} campaign";
        }

        return Ok(response);
    }

    [HttpPost("{id:guid}/send-to-client")]
    public async Task<IActionResult> SendToClient(Guid id, [FromBody] SendToClientRequest request, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.CampaignRecommendations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var recommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Recommendation not found.");

        recommendation.Status = "sent_to_client";
        recommendation.UpdatedAt = DateTime.UtcNow;
        campaign.Status = "review_ready";
        campaign.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Accepted(new { CampaignId = id, Message = "Recommendation sent to client.", ClientMessage = request.Message });
    }

    private static string ResolveQueueStage(Campaign campaign)
    {
        var latestRecommendation = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var hasRecommendation = latestRecommendation is not null;
        var recommendationStatus = latestRecommendation?.Status?.Trim().ToLowerInvariant();

        return campaign.Status switch
        {
            "paid" => "newly_paid",
            "brief_in_progress" => "brief_waiting",
            "brief_submitted" => "planning_ready",
            _ when recommendationStatus == "approved" => "completed",
            _ when recommendationStatus == "sent_to_client" => "waiting_on_client",
            "planning_in_progress" when hasRecommendation => "agent_review",
            "planning_in_progress" => "planning_ready",
            "review_ready" => "waiting_on_client",
            _ => "watching"
        };
    }

    private static string GetQueueLabel(string stage)
    {
        return stage switch
        {
            "newly_paid" => "Newly paid",
            "brief_waiting" => "Brief in progress",
            "planning_ready" => "Needs planning",
            "agent_review" => "Needs agent review",
            "ready_to_send" => "Ready to send",
            "waiting_on_client" => "Waiting on client",
            "completed" => "Completed",
            _ => "Watching"
        };
    }

    private static string GetAgentNextAction(Campaign campaign, string stage, Guid currentUserId)
    {
        var assignmentPrefix = campaign.AssignedAgentUserId switch
        {
            null => "Unassigned. Claim this campaign and",
            var assignedAgentId when assignedAgentId == currentUserId => "Assigned to you. Next,",
            _ => $"Assigned to {campaign.AssignedAgentUser?.FullName ?? "another agent"}. Monitor and"
        };

        return stage switch
        {
            "newly_paid" => $"{assignmentPrefix} check the order and wait for the client brief.",
            "brief_waiting" => $"{assignmentPrefix} monitor the brief and step in if the client needs help.",
            "planning_ready" => $"{assignmentPrefix} open the campaign and create the recommendation.",
            "agent_review" => $"{assignmentPrefix} review the AI draft, adjust the plan, and approve it before sending.",
            "ready_to_send" => $"{assignmentPrefix} review the recommendation and send it to the client.",
            "waiting_on_client" => $"{assignmentPrefix} wait for client approval or update requests.",
            "completed" => $"{assignmentPrefix} archive this work or support activation if needed.",
            _ => $"{assignmentPrefix} monitor campaign progress for {campaign.PackageBand.Name}."
        };
    }

    private static int GetQueueRank(string stage)
    {
        return stage switch
        {
            "newly_paid" => 0,
            "planning_ready" => 1,
            "agent_review" => 2,
            "ready_to_send" => 3,
            "brief_waiting" => 4,
            "waiting_on_client" => 5,
            "completed" => 6,
            _ => 7
        };
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

    private static string MergeClientFeedback(string notes, string? clientFeedbackNotes)
    {
        if (string.IsNullOrWhiteSpace(clientFeedbackNotes))
        {
            return notes;
        }

        return $"{notes.Trim()}\n\n{ClientFeedbackMarker} {clientFeedbackNotes}".Trim();
    }
}

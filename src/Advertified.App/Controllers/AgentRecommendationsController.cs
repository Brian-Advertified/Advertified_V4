using Advertified.App.Contracts.Agent;
using Advertified.App.Campaigns;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/recommendations")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentRecommendationsController : ControllerBase
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ILogger<AgentRecommendationsController> _logger;

    public AgentRecommendationsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAgentCampaignOwnershipService ownershipService,
        IChangeAuditService changeAuditService,
        ILogger<AgentRecommendationsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _ownershipService = ownershipService;
        _changeAuditService = changeAuditService;
        _logger = logger;
    }

    [HttpPost("/agent/campaigns/{campaignId:guid}/recommendations")]
    public async Task<IActionResult> Create(Guid campaignId, [FromBody] AgentRecommendationRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;

        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            campaignId,
            currentUser,
            query => query
                .Include(x => x.CampaignRecommendations)
                    .ThenInclude(x => x.RecommendationItems),
            cancellationToken);

        if (campaign.Status is CampaignStatuses.Approved
            or CampaignStatuses.CreativeChangesRequested
            or CampaignStatuses.CreativeSentToClientForApproval
            or CampaignStatuses.CreativeApproved
            or CampaignStatuses.BookingInProgress
            or CampaignStatuses.Launched
            || campaign.CampaignRecommendations.Any(x => string.Equals(x.Status, RecommendationStatuses.Approved, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { Message = "This campaign has already been approved and can no longer be edited from the recommendation workspace." });
        }

        var currentRecommendations = RecommendationRevisionSupport.GetCurrentRecommendationSet(campaign.CampaignRecommendations);
        if (currentRecommendations.Any(x => string.Equals(x.Status, RecommendationStatuses.Draft, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { Message = "A draft recommendation already exists. Update the existing draft instead." });
        }

        var now = DateTime.UtcNow;
        var notes = request.Notes?.Trim() ?? string.Empty;

        RecommendationOohPolicy.EnsureSelectedInventoryContainsOoh(request.InventoryItems ?? Array.Empty<SelectedInventoryItemRequest>());

        var recommendationId = Guid.NewGuid();
        var recommendation = new Data.Entities.CampaignRecommendation
        {
            Id = recommendationId,
            CampaignId = campaignId,
            RecommendationType = "manual:balanced",
            GeneratedBy = "agent_manual",
            Status = RecommendationStatuses.Draft,
            Summary = notes,
            Rationale = notes,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
            RevisionNumber = RecommendationRevisionSupport.GetNextRevisionNumber(campaign.CampaignRecommendations)
        };

        var items = BuildRecommendationItems(
            recommendationId,
            request.InventoryItems ?? Array.Empty<SelectedInventoryItemRequest>(),
            now);

        recommendation.TotalCost = items.Sum(x => x.TotalCost);
        _db.CampaignRecommendations.Add(recommendation);
        _db.RecommendationItems.AddRange(items);

        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "create_recommendation",
            recommendation.Id.ToString(),
            campaign.CampaignName,
            $"Created draft recommendation for {ResolveCampaignLabel(campaign)}.",
            new { RecommendationId = recommendation.Id, CampaignId = campaignId },
            cancellationToken);

        return Accepted(new { RecommendationId = recommendation.Id, Message = "Draft recommendation created." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var recommendation = await _db.CampaignRecommendations
            .Include(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Recommendation not found.");
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            recommendation.CampaignId,
            currentUser,
            query => query,
            cancellationToken);

        if (!string.Equals(recommendation.Status, RecommendationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft recommendations can be deleted.");
        }

        _db.RecommendationItems.RemoveRange(recommendation.RecommendationItems);
        _db.CampaignRecommendations.Remove(recommendation);

        if (campaign is not null)
        {
            campaign.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "delete_recommendation",
            recommendation.Id.ToString(),
            campaign?.CampaignName,
            $"Deleted draft recommendation for {ResolveCampaignLabel(campaign)}.",
            new { RecommendationId = recommendation.Id, CampaignId = recommendation.CampaignId },
            cancellationToken);

        return Ok(new { RecommendationId = id, Message = "Draft recommendation deleted." });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRecommendationRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var recommendation = await _db.CampaignRecommendations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Recommendation not found.");
        _ = await _ownershipService.GetOwnedCampaignAsync(
            recommendation.CampaignId,
            currentUser,
            query => query,
            cancellationToken);

        if (!string.Equals(recommendation.Status, RecommendationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                Message = "Only draft recommendations can be edited. Create a new revision instead."
            });
        }

        var notes = request.Notes?.Trim() ?? string.Empty;

        recommendation.Status = string.IsNullOrWhiteSpace(request.Status)
            ? recommendation.Status
            : request.Status;
        recommendation.Summary = notes;
        recommendation.Rationale = MergeClientFeedback(notes, ExtractClientFeedbackNotes(recommendation.Rationale));
        recommendation.UpdatedAt = DateTime.UtcNow;
        List<Data.Entities.RecommendationItem> updatedItems = new();
        try
        {
            RecommendationOohPolicy.EnsureSelectedInventoryContainsOoh(request.InventoryItems ?? Array.Empty<SelectedInventoryItemRequest>());
            updatedItems = BuildRecommendationItems(recommendation.Id, request.InventoryItems ?? Array.Empty<SelectedInventoryItemRequest>(), recommendation.UpdatedAt);
            await _db.RecommendationItems
                .Where(x => x.RecommendationId == recommendation.Id)
                .ExecuteDeleteAsync(cancellationToken);
            _db.RecommendationItems.AddRange(updatedItems);
            recommendation.TotalCost = updatedItems.Sum(x => x.TotalCost);
            if (recommendation.TotalCost <= 0)
            {
                recommendation.TotalCost = 0;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { Message = "Recommendation changed while saving. Refresh and try again." });
        }

        var campaignLabel = await _db.Campaigns
            .AsNoTracking()
            .Where(x => x.Id == recommendation.CampaignId)
            .Select(x => x.CampaignName)
            .FirstOrDefaultAsync(cancellationToken);

        try
        {
            await WriteChangeAuditAsync(
                "update_recommendation",
                recommendation.Id.ToString(),
                campaignLabel,
                $"Updated draft recommendation for {ResolveCampaignLabel(campaignLabel)}.",
                new
                {
                    RecommendationId = recommendation.Id,
                    CampaignId = recommendation.CampaignId,
                    recommendation.Status,
                    ItemCount = updatedItems.Count
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recommendation {RecommendationId} updated but audit logging failed.", recommendation.Id);
        }

        return Ok(new { RecommendationId = id, recommendation.Status, request.Notes });
    }

    private async Task WriteChangeAuditAsync(
        string action,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _changeAuditService.WriteAsync(currentUserId, "agent", action, "recommendation", entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private async Task<Data.Entities.UserAccount> GetCurrentOperationsUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (currentUser.Role is not Data.Enums.UserRole.Agent and not Data.Enums.UserRole.Admin and not Data.Enums.UserRole.CreativeDirector)
        {
            throw new ForbiddenException("Agent, creative director, or admin access is required.");
        }

        return currentUser;
    }

    private static string ResolveCampaignLabel(Data.Entities.Campaign? campaign)
    {
        return ResolveCampaignLabel(campaign?.CampaignName);
    }

    private static string ResolveCampaignLabel(string? campaignName)
    {
        return string.IsNullOrWhiteSpace(campaignName) ? "campaign" : campaignName.Trim();
    }

    private static List<Data.Entities.RecommendationItem> BuildRecommendationItems(
        Guid recommendationId,
        IReadOnlyList<SelectedInventoryItemRequest> inventoryItems,
        DateTime now)
    {
        var result = new List<Data.Entities.RecommendationItem>(inventoryItems.Count);

        foreach (var item in inventoryItems)
        {
            var quantity = item.Quantity <= 0 ? 1 : item.Quantity;
            if (item.Rate < 0)
            {
                throw new ArgumentException("Inventory rate cannot be negative.");
            }

            if (item.Rate > 9999999999.99m)
            {
                throw new ArgumentException("Inventory rate is too large.");
            }

            var totalCost = item.Rate * quantity;
            if (totalCost > 9999999999.99m)
            {
                throw new ArgumentException("Inventory total cost is too large.");
            }

            result.Add(new Data.Entities.RecommendationItem
            {
                Id = Guid.NewGuid(),
                RecommendationId = recommendationId,
                InventoryType = TrimToLength(string.IsNullOrWhiteSpace(item.Type) ? "base" : item.Type.Trim().ToLowerInvariant(), 50),
                DisplayName = TrimToLength(string.IsNullOrWhiteSpace(item.Station) ? "Selected inventory" : item.Station.Trim(), 255),
                Quantity = quantity,
                UnitCost = item.Rate,
                TotalCost = totalCost,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    sourceInventoryId = item.Id,
                    rationale = BuildInventoryRationale(item, quantity),
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

        return result;
    }

    private static string BuildInventoryRationale(SelectedInventoryItemRequest item, int quantity)
    {
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

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}

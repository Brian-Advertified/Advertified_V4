using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/recommendations")]
public sealed class AgentRecommendationsController : ControllerBase
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IChangeAuditService _changeAuditService;

    public AgentRecommendationsController(AppDbContext db, ICurrentUserAccessor currentUserAccessor, IChangeAuditService changeAuditService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _changeAuditService = changeAuditService;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var recommendation = await _db.CampaignRecommendations
            .Include(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Recommendation not found.");

        if (!string.Equals(recommendation.Status, RecommendationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft recommendations can be deleted.");
        }

        var campaign = await _db.Campaigns.FirstOrDefaultAsync(x => x.Id == recommendation.CampaignId, cancellationToken);

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
        var recommendation = await _db.CampaignRecommendations
            .Include(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Recommendation not found.");

        if (!string.Equals(recommendation.Status, RecommendationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft recommendations can be edited. Create a new revision instead.");
        }

        recommendation.Status = string.IsNullOrWhiteSpace(request.Status)
            ? recommendation.Status
            : request.Status;
        recommendation.Summary = request.Notes;
        recommendation.Rationale = MergeClientFeedback(request.Notes, ExtractClientFeedbackNotes(recommendation.Rationale));
        recommendation.UpdatedAt = DateTime.UtcNow;
        SyncRecommendationItems(recommendation, request.InventoryItems, recommendation.UpdatedAt);
        recommendation.TotalCost = recommendation.RecommendationItems.Sum(x => x.TotalCost);
        if (recommendation.TotalCost <= 0)
        {
            recommendation.TotalCost = 0;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var campaignLabel = await _db.Campaigns
            .AsNoTracking()
            .Where(x => x.Id == recommendation.CampaignId)
            .Select(x => x.CampaignName)
            .FirstOrDefaultAsync(cancellationToken);

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
                ItemCount = recommendation.RecommendationItems.Count
            },
            cancellationToken);

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

    private static string ResolveCampaignLabel(Data.Entities.Campaign? campaign)
    {
        return ResolveCampaignLabel(campaign?.CampaignName);
    }

    private static string ResolveCampaignLabel(string? campaignName)
    {
        return string.IsNullOrWhiteSpace(campaignName) ? "campaign" : campaignName.Trim();
    }

    private static void SyncRecommendationItems(Data.Entities.CampaignRecommendation recommendation, IReadOnlyList<SelectedInventoryItemRequest> inventoryItems, DateTime now)
    {
        recommendation.RecommendationItems.Clear();

        foreach (var item in inventoryItems)
        {
            var quantity = item.Quantity <= 0 ? 1 : item.Quantity;
            recommendation.RecommendationItems.Add(new Data.Entities.RecommendationItem
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
}

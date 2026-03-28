using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
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

    public AgentRecommendationsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRecommendationRequest request, CancellationToken cancellationToken)
    {
        var recommendation = await _db.CampaignRecommendations
            .Include(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Recommendation not found.");

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

        return Ok(new { RecommendationId = id, recommendation.Status, request.Notes });
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

using System.Text.Json;
using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/inventory")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentInventoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPlanningInventoryRepository _inventoryRepository;
    private readonly IPlanningEligibilityService _planningEligibilityService;
    private readonly IPlanningRequestFactory _planningRequestFactory;

    public AgentInventoryController(
        AppDbContext db,
        IPlanningInventoryRepository inventoryRepository,
        IPlanningEligibilityService planningEligibilityService,
        IPlanningRequestFactory planningRequestFactory)
    {
        _db = db;
        _inventoryRepository = inventoryRepository;
        _planningEligibilityService = planningEligibilityService;
        _planningRequestFactory = planningRequestFactory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentInventoryItemResponse>>> Get([FromQuery] Guid? campaignId, CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(campaignId, cancellationToken);
        var candidates = new List<InventoryCandidate>();

        candidates.AddRange(await _inventoryRepository.GetOohCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetDigitalCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetRadioSlotCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetRadioPackageCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetTvCandidatesAsync(request, cancellationToken));

        var eligibleCandidates = _planningEligibilityService.FilterEligibleCandidates(candidates, request).Candidates;

        var filtered = eligibleCandidates
            .Where(candidate => MatchesPreferredMedia(candidate, request))
            .OrderBy(candidate => GetChannelRank(candidate.MediaType))
            .ThenBy(candidate => candidate.Cost)
            .ThenBy(candidate => candidate.DisplayName)
            .GroupBy(candidate => candidate.SourceId)
            .Select(group => group.First())
            .ToArray();

        const int maxItems = 500;
        const int maxOohLikeItems = 350;
        const int maxRadioItems = 130;
        const int maxTvItems = 20;

        var oohLike = filtered
            .Where(candidate => PlanningChannelSupport.IsOohFamilyChannel(candidate.MediaType)
                                || candidate.MediaType.Equals("digital", StringComparison.OrdinalIgnoreCase))
            .Take(maxOohLikeItems);
        var radio = filtered
            .Where(candidate => candidate.MediaType.Equals("radio", StringComparison.OrdinalIgnoreCase))
            .Take(maxRadioItems);
        var tv = filtered
            .Where(candidate => candidate.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
            .Take(maxTvItems);

        var results = oohLike
            .Concat(radio)
            .Concat(tv)
            .OrderBy(candidate => GetChannelRank(candidate.MediaType))
            .ThenBy(candidate => candidate.Cost)
            .ThenBy(candidate => candidate.DisplayName)
            .Take(maxItems)
            .Select(MapInventoryItem)
            .ToArray();

        return Ok(results);
    }

    private async Task<CampaignPlanningRequest> BuildRequestAsync(Guid? campaignId, CancellationToken cancellationToken)
    {
        if (campaignId is null)
        {
            return new CampaignPlanningRequest
            {
                SelectedBudget = 1_000_000m,
                GeographyScope = "national"
            };
        }

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .FirstOrDefaultAsync(x => x.Id == campaignId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var brief = campaign.CampaignBrief;
        if (brief is null)
        {
            return new CampaignPlanningRequest
            {
                CampaignId = campaign.Id,
                SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                    campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                    campaign.PackageOrder.AiStudioReserveAmount),
                GeographyScope = "national"
            };
        }

        var packageProfile = campaign.PackageBandId == Guid.Empty
            ? null
            : await _db.PackageBandProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PackageBandId == campaign.PackageBandId, cancellationToken);

        return _planningRequestFactory.FromCampaignBrief(campaign, brief, request: null, packageProfile);
    }

    private static AgentInventoryItemResponse MapInventoryItem(InventoryCandidate candidate)
    {
        return new AgentInventoryItemResponse
        {
            Id = candidate.SourceId.ToString(),
            Type = NormalizeType(candidate.MediaType),
            Station = candidate.DisplayName,
            Region = BuildRegion(candidate),
            Language = candidate.Language ?? "Not specified",
            ShowDaypart = candidate.DayType ?? candidate.Area ?? "Not specified",
            TimeBand = candidate.TimeBand ?? "Not specified",
            SlotType = candidate.SlotType ?? "Not specified",
            Duration = BuildDuration(candidate),
            Rate = candidate.Cost,
            Restrictions = BuildRestrictions(candidate)
        };
    }

    private static string NormalizeType(string mediaType)
    {
        return PlanningChannelSupport.NormalizeChannel(mediaType);
    }

    private static string BuildRegion(InventoryCandidate candidate)
    {
        return string.Join(", ", new[] { candidate.Area, candidate.City, candidate.Province }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase))
            switch
            {
                { Length: > 0 } region => region,
                _ => candidate.MarketScope ?? "Not specified"
            };
    }

    private static string BuildDuration(InventoryCandidate candidate)
    {
        if (candidate.DurationSeconds.HasValue && candidate.DurationSeconds.Value > 0)
        {
            return $"{candidate.DurationSeconds.Value}s";
        }

        if (TryGetMetadataString(candidate.Metadata, "duration") is { Length: > 0 } duration)
        {
            return duration;
        }

        return "Not specified";
    }

    private static string BuildRestrictions(InventoryCandidate candidate)
    {
        return TryGetMetadataString(candidate.Metadata, "restrictions")
            ?? TryGetMetadataString(candidate.Metadata, "restrictionNotes")
            ?? "Not specified";
    }

    private static bool MatchesPreferredMedia(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.PreferredMediaTypes.Count == 0)
        {
            return true;
        }

        return request.PreferredMediaTypes.Any(value => PlanningChannelSupport.MatchesRequestedChannel(candidate.MediaType, value));
    }
    private static int GetChannelRank(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "billboard" => 0,
            "digital_screen" => 1,
            "radio" => 1,
            "tv" => 2,
            _ => 3
        };
    }

    private static string? TryGetMetadataString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }
}

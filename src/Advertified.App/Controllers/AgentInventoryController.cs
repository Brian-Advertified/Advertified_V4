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
    private const double LocalSuburbRadiusKm = 30.0;
    private readonly AppDbContext _db;
    private readonly IPlanningInventoryRepository _inventoryRepository;
    private readonly IGeocodingService _geocodingService;

    public AgentInventoryController(AppDbContext db, IPlanningInventoryRepository inventoryRepository, IGeocodingService geocodingService)
    {
        _db = db;
        _inventoryRepository = inventoryRepository;
        _geocodingService = geocodingService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentInventoryItemResponse>>> Get([FromQuery] Guid? campaignId, CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(campaignId, cancellationToken);
        var geocodingTarget = _geocodingService.ResolveCampaignTarget(request);
        if (geocodingTarget.IsResolved)
        {
            request.TargetLatitude = geocodingTarget.Latitude;
            request.TargetLongitude = geocodingTarget.Longitude;
        }
        var candidates = new List<InventoryCandidate>();

        candidates.AddRange(await _inventoryRepository.GetOohCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetDigitalCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetRadioSlotCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetRadioPackageCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetTvCandidatesAsync(request, cancellationToken));

        var filtered = candidates
            .Where(candidate => MatchesPreferredMedia(candidate, request))
            .Where(candidate => MatchesRequestedGeography(candidate, request))
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
            .Where(candidate => candidate.MediaType.Equals("ooh", StringComparison.OrdinalIgnoreCase)
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
        var strategyRequest = new CampaignPlanningRequest
        {
            BusinessStage = brief?.BusinessStage,
            MonthlyRevenueBand = brief?.MonthlyRevenueBand,
            SalesModel = brief?.SalesModel,
            CustomerType = brief?.CustomerType,
            BuyingBehaviour = brief?.BuyingBehaviour,
            DecisionCycle = brief?.DecisionCycle,
            PricePositioning = brief?.PricePositioning,
            AverageCustomerSpendBand = brief?.AverageCustomerSpendBand,
            GrowthTarget = brief?.GrowthTarget,
            UrgencyLevel = brief?.UrgencyLevel,
            AudienceClarity = brief?.AudienceClarity,
            ValuePropositionFocus = brief?.ValuePropositionFocus
        };
        var inferredLsmRange = CampaignStrategySupport.ResolveSuggestedLsmRange(strategyRequest);
        return new CampaignPlanningRequest
        {
            CampaignId = campaign.Id,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
            Objective = brief?.Objective,
            BusinessStage = brief?.BusinessStage,
            MonthlyRevenueBand = brief?.MonthlyRevenueBand,
            SalesModel = brief?.SalesModel,
            GeographyScope = brief?.GeographyScope,
            Provinces = DeserializeList(brief?.ProvincesJson),
            Cities = DeserializeList(brief?.CitiesJson),
            Suburbs = DeserializeList(brief?.SuburbsJson),
            Areas = DeserializeList(brief?.AreasJson),
            PreferredMediaTypes = DeserializeList(brief?.PreferredMediaTypesJson),
            ExcludedMediaTypes = DeserializeList(brief?.ExcludedMediaTypesJson),
            TargetLanguages = DeserializeList(brief?.TargetLanguagesJson),
            TargetAgeMin = brief?.TargetAgeMin,
            TargetAgeMax = brief?.TargetAgeMax,
            TargetGender = brief?.TargetGender,
            TargetInterests = DeserializeList(brief?.TargetInterestsJson)
                .Concat(CampaignStrategySupport.BuildAudienceTerms(strategyRequest))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TargetAudienceNotes = string.Join(
                Environment.NewLine,
                new[] { brief?.TargetAudienceNotes }
                    .Concat(CampaignStrategySupport.BuildContextLines(strategyRequest))
                    .Where(static value => !string.IsNullOrWhiteSpace(value))),
            CustomerType = brief?.CustomerType,
            BuyingBehaviour = brief?.BuyingBehaviour,
            DecisionCycle = brief?.DecisionCycle,
            PricePositioning = brief?.PricePositioning,
            AverageCustomerSpendBand = brief?.AverageCustomerSpendBand,
            GrowthTarget = brief?.GrowthTarget,
            UrgencyLevel = brief?.UrgencyLevel,
            AudienceClarity = brief?.AudienceClarity,
            ValuePropositionFocus = brief?.ValuePropositionFocus,
            TargetLsmMin = brief?.TargetLsmMin ?? inferredLsmRange.Min,
            TargetLsmMax = brief?.TargetLsmMax ?? inferredLsmRange.Max,
            OpenToUpsell = brief?.OpenToUpsell ?? false,
            AdditionalBudget = brief?.AdditionalBudget,
            MaxMediaItems = brief?.MaxMediaItems
        };
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
        var normalized = mediaType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "radio" => "radio",
            "ooh" => "ooh",
            "tv" => "tv",
            _ => normalized
        };
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

        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        return request.PreferredMediaTypes.Any(value => value.Equals(mediaType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesRequestedGeography(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var normalizedScope = NormalizeScope(request.GeographyScope);
        if (normalizedScope == "national")
        {
            return true;
        }

        var isBroadcast = candidate.MediaType.Equals("radio", StringComparison.OrdinalIgnoreCase)
            || candidate.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase);
        var isOohLike = candidate.MediaType.Equals("ooh", StringComparison.OrdinalIgnoreCase)
            || candidate.MediaType.Equals("digital", StringComparison.OrdinalIgnoreCase);

        var requestedTerms = (normalizedScope == "local"
                ? (request.Suburbs.Count > 0
                    ? (isBroadcast ? request.Cities : request.Suburbs)
                    : request.Areas.Concat(request.Cities))
                : request.Provinces)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (requestedTerms.Length == 0)
        {
            return true;
        }

        if (normalizedScope == "local"
            && request.Suburbs.Count > 0
            && isOohLike
            && request.TargetLatitude.HasValue
            && request.TargetLongitude.HasValue
            && candidate.Latitude.HasValue
            && candidate.Longitude.HasValue)
        {
            var distanceKm = HaversineDistanceKm(
                request.TargetLatitude.Value,
                request.TargetLongitude.Value,
                candidate.Latitude.Value,
                candidate.Longitude.Value);

            if (distanceKm <= LocalSuburbRadiusKm)
            {
                return true;
            }
        }

        var haystack = string.Join(" ", new[]
        {
            candidate.DisplayName,
            candidate.Area,
            candidate.City,
            candidate.Province,
            candidate.MarketScope
        }.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();

        return requestedTerms.Any(term => haystack.Contains(term));
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radiusKm = 6371.0;
        static double ToRadians(double angle) => Math.PI * angle / 180.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return radiusKm * c;
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = (scope ?? string.Empty).Trim().ToLowerInvariant();
        return normalized == "regional" ? "provincial" : normalized;
    }

    private static int GetChannelRank(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "ooh" => 0,
            "radio" => 1,
            "tv" => 2,
            _ => 3
        };
    }

    private static List<string> DeserializeList(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
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

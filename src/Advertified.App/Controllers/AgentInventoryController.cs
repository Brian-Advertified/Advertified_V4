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
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
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
    public async Task<ActionResult<IReadOnlyList<AgentInventoryItemResponse>>> Get(
        [FromQuery] Guid? campaignId,
        [FromQuery] Guid? recommendationId,
        CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(campaignId, recommendationId, cancellationToken);
        var candidates = new List<InventoryCandidate>();

        candidates.AddRange(await _inventoryRepository.GetOohCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetDigitalCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetRadioSlotCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetRadioPackageCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetTvCandidatesAsync(request, cancellationToken));
        candidates.AddRange(await _inventoryRepository.GetNewspaperCandidatesAsync(request, cancellationToken));

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
        const int maxNewspaperItems = 50;

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
        var newspapers = filtered
            .Where(candidate => candidate.MediaType.Equals("newspaper", StringComparison.OrdinalIgnoreCase))
            .Take(maxNewspaperItems);

        var results = oohLike
            .Concat(radio)
            .Concat(tv)
            .Concat(newspapers)
            .OrderBy(candidate => GetChannelRank(candidate.MediaType))
            .ThenBy(candidate => candidate.Cost)
            .ThenBy(candidate => candidate.DisplayName)
            .Take(maxItems)
            .Select(MapInventoryItem)
            .ToArray();

        return Ok(results);
    }

    private async Task<CampaignPlanningRequest> BuildRequestAsync(Guid? campaignId, Guid? recommendationId, CancellationToken cancellationToken)
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

        var request = _planningRequestFactory.FromCampaignBrief(campaign, brief, request: null, packageProfile);
        if (!recommendationId.HasValue)
        {
            return request;
        }

        var recommendation = await _db.CampaignRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == recommendationId.Value && x.CampaignId == campaign.Id,
                cancellationToken);

        if (recommendation is null || string.IsNullOrWhiteSpace(recommendation.RequestSnapshotJson))
        {
            return request;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<CampaignPlanningRequestSnapshot>(recommendation.RequestSnapshotJson, SnapshotJsonOptions);
            return snapshot is null ? request : ApplyRecommendationSnapshot(request, snapshot);
        }
        catch (JsonException)
        {
            return request;
        }
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

    private static CampaignPlanningRequest ApplyRecommendationSnapshot(
        CampaignPlanningRequest request,
        CampaignPlanningRequestSnapshot snapshot)
    {
        var clone = request.DeepClone();
        clone.SelectedBudget = snapshot.SelectedBudget > 0m ? snapshot.SelectedBudget : clone.SelectedBudget;
        clone.Objective = snapshot.Objective;
        clone.BusinessStage = snapshot.BusinessStage;
        clone.MonthlyRevenueBand = snapshot.MonthlyRevenueBand;
        clone.SalesModel = snapshot.SalesModel;
        clone.StartDate = snapshot.StartDate;
        clone.EndDate = snapshot.EndDate;
        clone.DurationWeeks = snapshot.DurationWeeks;
        clone.ChannelFlights = snapshot.ChannelFlights
            .Select(static flight => new CampaignChannelFlightRequest
            {
                Channel = flight.Channel,
                StartDate = flight.StartDate,
                EndDate = flight.EndDate,
                DurationWeeks = flight.DurationWeeks,
                DurationMonths = flight.DurationMonths,
                Priority = flight.Priority,
                Notes = flight.Notes
            })
            .ToList();
        clone.Industry = snapshot.Industry;
        clone.GeographyScope = snapshot.GeographyScope;
        clone.Provinces = snapshot.Provinces.ToList();
        clone.Cities = snapshot.Cities.ToList();
        clone.Suburbs = snapshot.Suburbs.ToList();
        clone.Areas = snapshot.Areas.ToList();
        clone.TargetLocationLabel = snapshot.TargetLocationLabel;
        clone.TargetLocationCity = snapshot.TargetLocationCity;
        clone.TargetLocationProvince = snapshot.TargetLocationProvince;
        clone.TargetLocationSource = snapshot.TargetLocationSource;
        clone.TargetLocationPrecision = snapshot.TargetLocationPrecision;
        clone.PreferredMediaTypes = snapshot.PreferredMediaTypes.ToList();
        clone.ExcludedMediaTypes = snapshot.ExcludedMediaTypes.ToList();
        clone.TargetLanguages = snapshot.TargetLanguages.ToList();
        clone.TargetAgeMin = snapshot.TargetAgeMin;
        clone.TargetAgeMax = snapshot.TargetAgeMax;
        clone.TargetGender = snapshot.TargetGender;
        clone.TargetInterests = snapshot.TargetInterests.ToList();
        clone.TargetAudienceNotes = snapshot.TargetAudienceNotes;
        clone.CustomerType = snapshot.CustomerType;
        clone.BuyingBehaviour = snapshot.BuyingBehaviour;
        clone.DecisionCycle = snapshot.DecisionCycle;
        clone.PricePositioning = snapshot.PricePositioning;
        clone.AverageCustomerSpendBand = snapshot.AverageCustomerSpendBand;
        clone.GrowthTarget = snapshot.GrowthTarget;
        clone.UrgencyLevel = snapshot.UrgencyLevel;
        clone.AudienceClarity = snapshot.AudienceClarity;
        clone.ValuePropositionFocus = snapshot.ValuePropositionFocus;
        clone.TargetLsmMin = snapshot.TargetLsmMin;
        clone.TargetLsmMax = snapshot.TargetLsmMax;
        clone.MustHaveAreas = snapshot.MustHaveAreas.ToList();
        clone.ExcludedAreas = snapshot.ExcludedAreas.ToList();
        clone.OpenToUpsell = snapshot.OpenToUpsell;
        clone.AdditionalBudget = snapshot.AdditionalBudget;
        clone.MaxMediaItems = snapshot.MaxMediaItems;
        clone.TargetRadioShare = snapshot.TargetRadioShare;
        clone.TargetOohShare = snapshot.TargetOohShare;
        clone.TargetTvShare = snapshot.TargetTvShare;
        clone.TargetDigitalShare = snapshot.TargetDigitalShare;
        clone.TargetNewspaperShare = snapshot.TargetNewspaperShare;
        clone.TargetLatitude = snapshot.TargetLatitude;
        clone.TargetLongitude = snapshot.TargetLongitude;
        clone.BusinessLocation = snapshot.BusinessLocation is null
            ? clone.BusinessLocation
            : new CampaignBusinessLocation
            {
                Label = snapshot.BusinessLocation.Label,
                Area = snapshot.BusinessLocation.Area,
                City = snapshot.BusinessLocation.City,
                Province = snapshot.BusinessLocation.Province,
                Latitude = snapshot.BusinessLocation.Latitude,
                Longitude = snapshot.BusinessLocation.Longitude,
                Source = snapshot.BusinessLocation.Source,
                Precision = snapshot.BusinessLocation.Precision,
                IsResolved = snapshot.BusinessLocation.IsResolved
            };
        clone.Targeting = snapshot.Targeting is null
            ? clone.Targeting
            : new CampaignTargetingProfile
            {
                Scope = snapshot.Targeting.Scope,
                Label = snapshot.Targeting.Label,
                City = snapshot.Targeting.City,
                Province = snapshot.Targeting.Province,
                Latitude = snapshot.Targeting.Latitude,
                Longitude = snapshot.Targeting.Longitude,
                Source = snapshot.Targeting.Source,
                Precision = snapshot.Targeting.Precision,
                Provinces = snapshot.Targeting.Provinces.ToList(),
                Cities = snapshot.Targeting.Cities.ToList(),
                Suburbs = snapshot.Targeting.Suburbs.ToList(),
                Areas = snapshot.Targeting.Areas.ToList(),
                PriorityAreas = snapshot.Targeting.PriorityAreas.ToList(),
                Exclusions = snapshot.Targeting.Exclusions.ToList()
            };
        clone.BudgetAllocation = snapshot.BudgetAllocation is null
            ? clone.BudgetAllocation
            : new PlanningBudgetAllocation
            {
                ChannelPolicyKey = snapshot.BudgetAllocation.ChannelPolicyKey,
                GeoPolicyKey = snapshot.BudgetAllocation.GeoPolicyKey,
                AudienceSegment = snapshot.BudgetAllocation.AudienceSegment,
                ChannelAllocations = snapshot.BudgetAllocation.ChannelAllocations
                    .Select(static allocation => new PlanningChannelAllocation
                    {
                        Channel = allocation.Channel,
                        Weight = allocation.Weight,
                        Amount = allocation.Amount
                    })
                    .ToList(),
                GeoAllocations = snapshot.BudgetAllocation.GeoAllocations
                    .Select(static allocation => new PlanningGeoAllocation
                    {
                        Bucket = allocation.Bucket,
                        Weight = allocation.Weight,
                        Amount = allocation.Amount,
                        RadiusKm = allocation.RadiusKm
                    })
                    .ToList(),
                CompositeAllocations = snapshot.BudgetAllocation.CompositeAllocations
                    .Select(static allocation => new PlanningAllocationLine
                    {
                        Channel = allocation.Channel,
                        Bucket = allocation.Bucket,
                        Weight = allocation.Weight,
                        Amount = allocation.Amount,
                        RadiusKm = allocation.RadiusKm
                    })
                    .ToList()
            };
        return clone;
    }
    private static int GetChannelRank(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "billboard" => 0,
            "digital_screen" => 1,
            "radio" => 1,
            "tv" => 2,
            "newspaper" => 3,
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

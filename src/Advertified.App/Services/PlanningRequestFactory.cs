using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class PlanningRequestFactory : IPlanningRequestFactory
{
    private readonly ICampaignPlanningTargetResolver _planningTargetResolver;
    private readonly ICampaignBusinessLocationResolver _businessLocationResolver;
    private readonly IPlanningBudgetAllocationService _budgetAllocationService;

    public PlanningRequestFactory(
        ICampaignPlanningTargetResolver planningTargetResolver,
        ICampaignBusinessLocationResolver businessLocationResolver,
        IPlanningBudgetAllocationService budgetAllocationService)
    {
        _planningTargetResolver = planningTargetResolver;
        _businessLocationResolver = businessLocationResolver;
        _budgetAllocationService = budgetAllocationService;
    }

    public CampaignPlanningRequest FromCampaignBrief(
        Campaign campaign,
        CampaignBrief brief,
        GenerateRecommendationRequest? request,
        PackageBandProfile? packageProfile)
    {
        var preferredMediaTypes = Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(
                brief,
                nameof(CampaignBrief.PreferredMediaTypesJson))
            .ToList();
        if (string.Equals(packageProfile?.IncludeTv, "yes", StringComparison.OrdinalIgnoreCase)
            && !preferredMediaTypes.Any(media =>
                string.Equals(media?.Trim(), "tv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(media?.Trim(), "television", StringComparison.OrdinalIgnoreCase)))
        {
            preferredMediaTypes.Add("tv");
        }

        var normalizedGeography = CampaignGeographyNormalizer.Normalize(
            brief.GeographyScope,
            Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.ProvincesJson)),
            Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.CitiesJson)),
            Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.SuburbsJson)),
            Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.AreasJson)));

        var strategyRequest = new CampaignPlanningRequest
        {
            BusinessStage = brief.BusinessStage,
            MonthlyRevenueBand = brief.MonthlyRevenueBand,
            SalesModel = brief.SalesModel,
            CustomerType = brief.CustomerType,
            BuyingBehaviour = brief.BuyingBehaviour,
            DecisionCycle = brief.DecisionCycle,
            PricePositioning = brief.PricePositioning,
            AverageCustomerSpendBand = brief.AverageCustomerSpendBand,
            GrowthTarget = brief.GrowthTarget,
            UrgencyLevel = brief.UrgencyLevel,
            AudienceClarity = brief.AudienceClarity,
            ValuePropositionFocus = brief.ValuePropositionFocus
        };

        var inferredLsmRange = Advertified.App.Domain.Campaigns.CampaignStrategySupport.ResolveSuggestedLsmRange(strategyRequest);
        var targetInterests = Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.TargetInterestsJson))
            .Concat(Advertified.App.Domain.Campaigns.CampaignStrategySupport.BuildAudienceTerms(strategyRequest))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetAudienceNotes = string.Join(
            Environment.NewLine,
            new[] { brief.TargetAudienceNotes }
                .Concat(Advertified.App.Domain.Campaigns.CampaignStrategySupport.BuildContextLines(strategyRequest))
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        var businessLocation = _businessLocationResolver.Resolve(campaign);
        var mustHaveAreas = Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.MustHaveAreasJson));
        var excludedAreas = Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.ExcludedAreasJson));

        var planningRequest = new CampaignPlanningRequest
        {
            CampaignId = campaign.Id,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
            BusinessLocation = new CampaignBusinessLocation
            {
                Label = businessLocation.Label,
                Area = businessLocation.Area,
                City = businessLocation.City,
                Province = businessLocation.Province,
                Latitude = businessLocation.Latitude,
                Longitude = businessLocation.Longitude,
                Source = businessLocation.Source,
                Precision = businessLocation.Precision,
                IsResolved = businessLocation.IsResolved
            },
            Objective = brief.Objective,
            BusinessStage = brief.BusinessStage,
            MonthlyRevenueBand = brief.MonthlyRevenueBand,
            SalesModel = brief.SalesModel,
            GeographyScope = normalizedGeography.Scope,
            Provinces = normalizedGeography.Provinces.ToList(),
            Cities = normalizedGeography.Cities.ToList(),
            Suburbs = normalizedGeography.Suburbs.ToList(),
            Areas = normalizedGeography.Areas.ToList(),
            TargetLocationLabel = brief.TargetLocationLabel,
            TargetLocationCity = brief.TargetLocationCity,
            TargetLocationProvince = brief.TargetLocationProvince,
            TargetLatitude = brief.TargetLatitude,
            TargetLongitude = brief.TargetLongitude,
            PreferredMediaTypes = preferredMediaTypes,
            ExcludedMediaTypes = Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.ExcludedMediaTypesJson)),
            TargetLanguages = Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.TargetLanguagesJson)),
            TargetAgeMin = brief.TargetAgeMin,
            TargetAgeMax = brief.TargetAgeMax,
            TargetGender = brief.TargetGender,
            TargetInterests = targetInterests,
            TargetAudienceNotes = string.IsNullOrWhiteSpace(targetAudienceNotes) ? null : targetAudienceNotes,
            CustomerType = brief.CustomerType,
            BuyingBehaviour = brief.BuyingBehaviour,
            DecisionCycle = brief.DecisionCycle,
            PricePositioning = brief.PricePositioning,
            AverageCustomerSpendBand = brief.AverageCustomerSpendBand,
            GrowthTarget = brief.GrowthTarget,
            UrgencyLevel = brief.UrgencyLevel,
            AudienceClarity = brief.AudienceClarity,
            ValuePropositionFocus = brief.ValuePropositionFocus,
            TargetLsmMin = brief.TargetLsmMin ?? inferredLsmRange.Min,
            TargetLsmMax = brief.TargetLsmMax ?? inferredLsmRange.Max,
            MustHaveAreas = mustHaveAreas,
            ExcludedAreas = excludedAreas,
            OpenToUpsell = brief.OpenToUpsell,
            AdditionalBudget = brief.AdditionalBudget,
            MaxMediaItems = brief.MaxMediaItems,
            TargetRadioShare = request?.TargetRadioShare,
            TargetOohShare = request?.TargetOohShare,
            TargetTvShare = request?.TargetTvShare,
            TargetDigitalShare = request?.TargetDigitalShare
        };

        var resolvedTarget = _planningTargetResolver.Resolve(planningRequest);
        planningRequest.TargetLocationLabel = resolvedTarget.Label;
        planningRequest.TargetLocationCity = resolvedTarget.City;
        planningRequest.TargetLocationProvince = resolvedTarget.Province;
        planningRequest.TargetLocationSource = resolvedTarget.Source;
        planningRequest.TargetLocationPrecision = resolvedTarget.Precision;
        if (resolvedTarget.IsResolved)
        {
            planningRequest.TargetLatitude = resolvedTarget.Latitude;
            planningRequest.TargetLongitude = resolvedTarget.Longitude;
        }

        planningRequest.Targeting = BuildTargetingProfile(planningRequest);
        planningRequest.BudgetAllocation = _budgetAllocationService.Resolve(planningRequest);

        return planningRequest;
    }

    private static CampaignTargetingProfile BuildTargetingProfile(CampaignPlanningRequest request)
    {
        var priorityAreas = new List<string>();
        priorityAreas.AddRange(request.MustHaveAreas);

        if (!string.Equals(request.GeographyScope, "local", StringComparison.OrdinalIgnoreCase))
        {
            AddIfMeaningful(priorityAreas, request.BusinessLocation?.Area);
            AddIfMeaningful(priorityAreas, request.BusinessLocation?.City);
        }

        if (priorityAreas.Count == 0)
        {
            AddIfMeaningful(priorityAreas, request.TargetLocationLabel);
        }

        return new CampaignTargetingProfile
        {
            Scope = request.GeographyScope ?? string.Empty,
            Label = request.TargetLocationLabel,
            City = request.TargetLocationCity,
            Province = request.TargetLocationProvince,
            Latitude = request.TargetLatitude,
            Longitude = request.TargetLongitude,
            Source = request.TargetLocationSource ?? "none",
            Precision = request.TargetLocationPrecision ?? "unknown",
            Provinces = request.Provinces.ToList(),
            Cities = request.Cities.ToList(),
            Suburbs = request.Suburbs.ToList(),
            Areas = request.Areas.ToList(),
            PriorityAreas = priorityAreas
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Exclusions = request.ExcludedAreas
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static void AddIfMeaningful(ICollection<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value.Trim());
        }
    }
}

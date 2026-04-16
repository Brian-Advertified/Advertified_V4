using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class PlanningRequestFactory : IPlanningRequestFactory
{
    private readonly ICampaignPlanningTargetResolver _planningTargetResolver;

    public PlanningRequestFactory(ICampaignPlanningTargetResolver planningTargetResolver)
    {
        _planningTargetResolver = planningTargetResolver;
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

        var planningRequest = new CampaignPlanningRequest
        {
            CampaignId = campaign.Id,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
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

        return planningRequest;
    }
}

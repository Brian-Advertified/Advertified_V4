using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class CampaignBriefMapper
{
    public static void Apply(CampaignBrief brief, SaveCampaignBriefRequest request, DateTime now)
    {
        var normalizedGeography = CampaignGeographyNormalizer.Normalize(
            request.GeographyScope,
            request.Provinces,
            request.Cities,
            request.Suburbs,
            request.Areas);

        brief.Objective = request.Objective;
        brief.BusinessStage = request.BusinessStage;
        brief.MonthlyRevenueBand = request.MonthlyRevenueBand;
        brief.SalesModel = request.SalesModel;
        brief.StartDate = request.StartDate;
        brief.EndDate = request.EndDate;
        brief.DurationWeeks = request.DurationWeeks;
        brief.GeographyScope = normalizedGeography.Scope;
        brief.ProvincesJson = Serialize(normalizedGeography.Provinces);
        brief.CitiesJson = Serialize(normalizedGeography.Cities);
        brief.SuburbsJson = Serialize(normalizedGeography.Suburbs);
        brief.AreasJson = Serialize(normalizedGeography.Areas);
        brief.TargetAgeMin = request.TargetAgeMin;
        brief.TargetAgeMax = request.TargetAgeMax;
        brief.TargetGender = request.TargetGender;
        brief.TargetLanguagesJson = Serialize(request.TargetLanguages);
        brief.TargetLsmMin = request.TargetLsmMin;
        brief.TargetLsmMax = request.TargetLsmMax;
        brief.TargetInterestsJson = Serialize(request.TargetInterests);
        brief.TargetAudienceNotes = request.TargetAudienceNotes;
        brief.CustomerType = request.CustomerType;
        brief.BuyingBehaviour = request.BuyingBehaviour;
        brief.DecisionCycle = request.DecisionCycle;
        brief.PricePositioning = request.PricePositioning;
        brief.AverageCustomerSpendBand = request.AverageCustomerSpendBand;
        brief.GrowthTarget = request.GrowthTarget;
        brief.UrgencyLevel = request.UrgencyLevel;
        brief.AudienceClarity = request.AudienceClarity;
        brief.ValuePropositionFocus = request.ValuePropositionFocus;
        brief.PreferredMediaTypesJson = Serialize(request.PreferredMediaTypes);
        brief.ExcludedMediaTypesJson = Serialize(request.ExcludedMediaTypes);
        brief.MustHaveAreasJson = Serialize(request.MustHaveAreas);
        brief.ExcludedAreasJson = Serialize(request.ExcludedAreas);
        brief.CreativeReady = request.CreativeReady;
        brief.CreativeNotes = request.CreativeNotes;
        brief.MaxMediaItems = request.MaxMediaItems;
        brief.OpenToUpsell = request.OpenToUpsell;
        brief.AdditionalBudget = request.AdditionalBudget;
        brief.SpecialRequirements = request.SpecialRequirements;
        brief.PreferredVideoAspectRatio = request.PreferredVideoAspectRatio;
        brief.PreferredVideoDurationSeconds = request.PreferredVideoDurationSeconds;
        brief.UpdatedAt = now;
    }

    private static string? Serialize<T>(T value)
    {
        return value == null ? null : JsonSerializer.Serialize(value);
    }
}

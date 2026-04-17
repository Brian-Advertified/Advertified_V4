using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class CampaignPlanningTargetResolver : ICampaignPlanningTargetResolver
{
    private readonly IGeocodingService _geocodingService;

    public CampaignPlanningTargetResolver(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    public CampaignPlanningTargetResolution Resolve(CampaignBrief? brief)
    {
        if (brief is null)
        {
            return new CampaignPlanningTargetResolution();
        }

        return ResolveCore(
            geographyScope: brief.GeographyScope,
            targetLocationLabel: brief.TargetLocationLabel,
            targetLocationCity: brief.TargetLocationCity,
            targetLocationProvince: brief.TargetLocationProvince,
            targetLatitude: brief.TargetLatitude,
            targetLongitude: brief.TargetLongitude,
            suburbs: Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.SuburbsJson)),
            cities: Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.CitiesJson)),
            areas: Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.AreasJson)),
            provinces: Advertified.App.Domain.Campaigns.CampaignBriefExtensions.GetList(brief, nameof(CampaignBrief.ProvincesJson)));
    }

    public CampaignPlanningTargetResolution Resolve(CampaignPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ResolveCore(
            geographyScope: request.Targeting?.Scope ?? request.GeographyScope,
            targetLocationLabel: request.Targeting?.Label ?? request.TargetLocationLabel,
            targetLocationCity: request.Targeting?.City ?? request.TargetLocationCity,
            targetLocationProvince: request.Targeting?.Province ?? request.TargetLocationProvince,
            targetLatitude: request.Targeting?.Latitude ?? request.TargetLatitude,
            targetLongitude: request.Targeting?.Longitude ?? request.TargetLongitude,
            suburbs: request.Targeting?.Suburbs ?? request.Suburbs,
            cities: request.Targeting?.Cities ?? request.Cities,
            areas: request.Targeting?.Areas ?? request.Areas,
            provinces: request.Targeting?.Provinces ?? request.Provinces);
    }

    private CampaignPlanningTargetResolution ResolveCore(
        string? geographyScope,
        string? targetLocationLabel,
        string? targetLocationCity,
        string? targetLocationProvince,
        double? targetLatitude,
        double? targetLongitude,
        IEnumerable<string> suburbs,
        IEnumerable<string> cities,
        IEnumerable<string> areas,
        IEnumerable<string> provinces)
    {
        var explicitLabel = NormalizeText(targetLocationLabel);
        var explicitCity = NormalizeText(targetLocationCity);
        var explicitProvince = NormalizeText(targetLocationProvince);
        var suburb = FirstMeaningful(suburbs);
        var city = FirstMeaningful(cities);
        var area = FirstMeaningful(areas);
        var province = FirstMeaningful(provinces);
        var fallbackLabel = explicitLabel
            ?? suburb
            ?? city
            ?? area
            ?? province
            ?? ResolveNationalFallback(geographyScope);

        if (string.IsNullOrWhiteSpace(fallbackLabel))
        {
            return new CampaignPlanningTargetResolution();
        }

        if (targetLatitude.HasValue && targetLongitude.HasValue)
        {
            return new CampaignPlanningTargetResolution
            {
                IsResolved = true,
                Label = fallbackLabel,
                City = explicitCity ?? city,
                Province = explicitProvince ?? province,
                Latitude = targetLatitude,
                Longitude = targetLongitude,
                Source = "stored_campaign_brief",
                Precision = ResolvePrecision(explicitLabel, suburb, city, area, province, geographyScope)
            };
        }

        foreach (var candidate in BuildLookupCandidates(explicitLabel, suburb, city, area, explicitCity, explicitProvince, province, geographyScope))
        {
            var resolution = _geocodingService.ResolveLocation(candidate);
            if (!resolution.IsResolved)
            {
                continue;
            }

            return new CampaignPlanningTargetResolution
            {
                IsResolved = true,
                Label = string.IsNullOrWhiteSpace(resolution.CanonicalLocation) ? fallbackLabel : resolution.CanonicalLocation,
                City = explicitCity ?? city,
                Province = explicitProvince ?? province,
                Latitude = resolution.Latitude,
                Longitude = resolution.Longitude,
                Source = resolution.Source,
                Precision = ResolvePrecision(explicitLabel, suburb, city, area, province, geographyScope)
            };
        }

        return new CampaignPlanningTargetResolution
        {
            IsResolved = false,
            Label = fallbackLabel,
            City = explicitCity ?? city,
            Province = explicitProvince ?? province,
            Latitude = targetLatitude,
            Longitude = targetLongitude,
            Source = "label_only",
            Precision = ResolvePrecision(explicitLabel, suburb, city, area, province, geographyScope)
        };
    }

    private static IEnumerable<string> BuildLookupCandidates(
        params string?[] values)
    {
        return values
            .Select(NormalizeText)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)!;
    }

    private static string ResolvePrecision(
        string? explicitLabel,
        string? suburb,
        string? city,
        string? area,
        string? province,
        string? geographyScope)
    {
        if (!string.IsNullOrWhiteSpace(explicitLabel) || !string.IsNullOrWhiteSpace(suburb))
        {
            return "local";
        }

        if (!string.IsNullOrWhiteSpace(city) || !string.IsNullOrWhiteSpace(area))
        {
            return "city";
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            return "provincial";
        }

        return string.Equals(geographyScope?.Trim(), "national", StringComparison.OrdinalIgnoreCase)
            ? "national"
            : "unknown";
    }

    private static string? ResolveNationalFallback(string? geographyScope)
    {
        return string.Equals(geographyScope?.Trim(), "national", StringComparison.OrdinalIgnoreCase)
            ? "South Africa"
            : null;
    }

    private static string? FirstMeaningful(IEnumerable<string> values)
    {
        return values
            .Select(NormalizeText)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

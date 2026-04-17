using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class GeocodingService : IGeocodingService
{
    private readonly ILeadMasterDataService _masterDataService;

    public GeocodingService(ILeadMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    public GeocodingResolution ResolveLocation(string? rawLocation)
    {
        if (string.IsNullOrWhiteSpace(rawLocation))
        {
            return new GeocodingResolution();
        }

        var masterMatch = _masterDataService.ResolveLocation(rawLocation);
        if (masterMatch is not null)
        {
            return new GeocodingResolution
            {
                IsResolved = masterMatch.Latitude.HasValue && masterMatch.Longitude.HasValue,
                CanonicalLocation = masterMatch.CanonicalName,
                Latitude = masterMatch.Latitude,
                Longitude = masterMatch.Longitude,
                Source = "master_locations"
            };
        }

        return new GeocodingResolution
        {
            IsResolved = false,
            CanonicalLocation = rawLocation.Trim(),
            Source = "none"
        };
    }

    public GeocodingResolution ResolveCampaignTarget(CampaignPlanningRequest request)
    {
        var targeting = request.Targeting;
        var latitude = targeting?.Latitude ?? request.TargetLatitude;
        var longitude = targeting?.Longitude ?? request.TargetLongitude;
        if (latitude.HasValue && longitude.HasValue)
        {
            return new GeocodingResolution
            {
                IsResolved = true,
                CanonicalLocation = "request_target",
                Latitude = latitude,
                Longitude = longitude,
                Source = "request"
            };
        }

        var candidates = (targeting?.Cities ?? request.Cities)
            .Concat(targeting?.Areas ?? request.Areas)
            .Concat(targeting?.Suburbs ?? request.Suburbs)
            .Concat(targeting?.Provinces ?? request.Provinces)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var resolution = ResolveLocation(candidate);
            if (resolution.IsResolved)
            {
                return resolution;
            }
        }

        return ResolveLocation(request.GeographyScope);
    }
}

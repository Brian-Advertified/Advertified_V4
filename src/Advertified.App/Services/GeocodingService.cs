using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class GeocodingService : IGeocodingService
{
    private static readonly Dictionary<string, (double Latitude, double Longitude)> FallbackCoordinates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["johannesburg"] = (-26.2041d, 28.0473d),
            ["pretoria"] = (-25.7479d, 28.2293d),
            ["cape town"] = (-33.9249d, 18.4241d),
            ["durban"] = (-29.8587d, 31.0218d),
            ["south africa"] = (-30.5595d, 22.9375d)
        };

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

        if (FallbackCoordinates.TryGetValue(rawLocation.Trim(), out var fallback))
        {
            return new GeocodingResolution
            {
                IsResolved = true,
                CanonicalLocation = rawLocation.Trim(),
                Latitude = fallback.Latitude,
                Longitude = fallback.Longitude,
                Source = "fallback"
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
        if (request.TargetLatitude.HasValue && request.TargetLongitude.HasValue)
        {
            return new GeocodingResolution
            {
                IsResolved = true,
                CanonicalLocation = "request_target",
                Latitude = request.TargetLatitude,
                Longitude = request.TargetLongitude,
                Source = "request"
            };
        }

        var candidates = request.Cities
            .Concat(request.Areas)
            .Concat(request.Suburbs)
            .Concat(request.Provinces)
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

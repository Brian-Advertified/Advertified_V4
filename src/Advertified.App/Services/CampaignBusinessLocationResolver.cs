using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class CampaignBusinessLocationResolver : ICampaignBusinessLocationResolver
{
    private readonly IGeocodingService _geocodingService;

    public CampaignBusinessLocationResolver(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    public CampaignBusinessLocationResolution Resolve(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var businessProfile = campaign.User?.BusinessProfile;
        if (businessProfile is null)
        {
            return new CampaignBusinessLocationResolution();
        }

        var city = NormalizeText(businessProfile.City);
        var province = NormalizeText(businessProfile.Province);
        var streetAddress = NormalizeText(businessProfile.StreetAddress);
        var fallbackLabel = string.Join(", ", new[] { city, province }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(fallbackLabel))
        {
            fallbackLabel = NormalizeText(businessProfile.BusinessName) ?? string.Empty;
        }

        foreach (var candidate in BuildLookupCandidates(streetAddress, city, province))
        {
            var resolution = _geocodingService.ResolveLocation(candidate);
            if (!resolution.IsResolved)
            {
                continue;
            }

            return new CampaignBusinessLocationResolution
            {
                IsResolved = true,
                Label = string.IsNullOrWhiteSpace(resolution.CanonicalLocation) ? fallbackLabel : resolution.CanonicalLocation,
                Area = ResolveArea(resolution.CanonicalLocation, city, province),
                City = city,
                Province = province,
                Latitude = resolution.Latitude,
                Longitude = resolution.Longitude,
                Source = resolution.Source,
                Precision = ResolvePrecision(resolution.CanonicalLocation, city, province)
            };
        }

        if (string.IsNullOrWhiteSpace(fallbackLabel))
        {
            return new CampaignBusinessLocationResolution();
        }

        return new CampaignBusinessLocationResolution
        {
            IsResolved = false,
            Label = fallbackLabel,
            Area = city,
            City = city,
            Province = province,
            Source = "business_profile",
            Precision = city is not null ? "city" : province is not null ? "provincial" : "unknown"
        };
    }

    private static IEnumerable<string> BuildLookupCandidates(string? streetAddress, string? city, string? province)
    {
        var combinations = new[]
        {
            JoinNonEmpty(", ", streetAddress, city, province),
            JoinNonEmpty(", ", city, province),
            city,
            province
        };

        return combinations
            .Select(NormalizeText)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)!;
    }

    private static string? ResolveArea(string? canonicalLocation, string? city, string? province)
    {
        var normalizedCanonical = NormalizeText(canonicalLocation);
        if (string.IsNullOrWhiteSpace(normalizedCanonical))
        {
            return city;
        }

        if (!string.IsNullOrWhiteSpace(city)
            && normalizedCanonical.Equals(city, StringComparison.OrdinalIgnoreCase))
        {
            return city;
        }

        if (!string.IsNullOrWhiteSpace(province)
            && normalizedCanonical.Equals(province, StringComparison.OrdinalIgnoreCase))
        {
            return city ?? province;
        }

        return normalizedCanonical;
    }

    private static string ResolvePrecision(string? canonicalLocation, string? city, string? province)
    {
        var normalizedCanonical = NormalizeText(canonicalLocation);
        if (!string.IsNullOrWhiteSpace(city)
            && string.Equals(normalizedCanonical, city, StringComparison.OrdinalIgnoreCase))
        {
            return "city";
        }

        if (!string.IsNullOrWhiteSpace(province)
            && string.Equals(normalizedCanonical, province, StringComparison.OrdinalIgnoreCase))
        {
            return "provincial";
        }

        return !string.IsNullOrWhiteSpace(normalizedCanonical) ? "local" : "unknown";
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        return string.Join(separator, values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
    }
}

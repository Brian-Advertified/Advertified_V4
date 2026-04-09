using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IGeocodingService
{
    GeocodingResolution ResolveLocation(string? rawLocation);

    GeocodingResolution ResolveCampaignTarget(CampaignPlanningRequest request);
}

public sealed class GeocodingResolution
{
    public bool IsResolved { get; init; }

    public string CanonicalLocation { get; init; } = string.Empty;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string Source { get; init; } = "none";
}

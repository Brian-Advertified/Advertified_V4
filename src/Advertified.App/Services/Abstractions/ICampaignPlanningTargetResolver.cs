using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignPlanningTargetResolver
{
    CampaignPlanningTargetResolution Resolve(CampaignBrief? brief);

    CampaignPlanningTargetResolution Resolve(CampaignPlanningRequest request);
}

public sealed class CampaignPlanningTargetResolution
{
    public bool IsResolved { get; init; }

    public string Label { get; init; } = string.Empty;

    public string? City { get; init; }

    public string? Province { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string Source { get; init; } = "none";

    public string Precision { get; init; } = "unknown";
}

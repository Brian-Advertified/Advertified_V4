using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface IMediaPlanningEngine
{
    Task<RecommendationResult> GenerateAsync(
        CampaignPlanningRequest request,
        CancellationToken cancellationToken);
}

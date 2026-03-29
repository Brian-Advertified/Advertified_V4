using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignRecommendationService
{
    Task<Guid> GenerateAndSaveAsync(Guid campaignId, GenerateRecommendationRequest? request, CancellationToken cancellationToken);
}

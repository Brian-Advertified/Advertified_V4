namespace Advertified.App.Services.Abstractions;

public interface ICampaignRecommendationService
{
    Task<Guid> GenerateAndSaveAsync(Guid campaignId, CancellationToken cancellationToken);
}

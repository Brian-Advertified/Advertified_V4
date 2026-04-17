using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface ILocationCatalogService
{
    Task SeedResolvedLocationAsync(SaveCampaignBriefRequest request, CancellationToken cancellationToken);
}

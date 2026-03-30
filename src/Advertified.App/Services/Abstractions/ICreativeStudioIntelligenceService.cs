using Advertified.App.Contracts.Creative;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services.Abstractions;

public interface ICreativeStudioIntelligenceService
{
    Task<CreativeSystemResponse> GenerateAsync(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request,
        CancellationToken cancellationToken);
}

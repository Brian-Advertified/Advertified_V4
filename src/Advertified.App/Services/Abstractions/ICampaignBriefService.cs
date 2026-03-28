using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignBriefService
{
    Task SaveDraftAsync(Guid userId, Guid campaignId, SaveCampaignBriefRequest request, CancellationToken cancellationToken);
    Task SubmitAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken);
    Task SetPlanningModeAsync(Guid userId, Guid campaignId, string planningMode, CancellationToken cancellationToken);
}

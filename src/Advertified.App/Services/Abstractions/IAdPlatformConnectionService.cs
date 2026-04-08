using Advertified.App.AIPlatform.Api;

namespace Advertified.App.Services.Abstractions;

public interface IAdPlatformConnectionService
{
    Task<IReadOnlyList<CampaignAdPlatformConnectionResponse>> GetCampaignConnectionsAsync(Guid campaignId, CancellationToken cancellationToken);
    Task<CampaignAdPlatformConnectionResponse> UpsertCampaignConnectionAsync(
        Guid campaignId,
        Guid? ownerUserId,
        UpsertCampaignAdPlatformConnectionRequest request,
        CancellationToken cancellationToken);
}


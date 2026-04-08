using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IAdPlatformAccessTokenService
{
    Task<string?> ResolveAccessTokenAsync(
        CampaignAdPlatformLink? link,
        string platform,
        CancellationToken cancellationToken);
}


using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IAgentCampaignOwnershipService
{
    IQueryable<Campaign> ApplyReadableScope(IQueryable<Campaign> query, UserAccount currentUser);
    IQueryable<Campaign> ApplyOwnedOrClaimableScope(IQueryable<Campaign> query, UserAccount currentUser);
    Task<Campaign> GetReadableCampaignAsync(Guid campaignId, UserAccount currentUser, Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder, CancellationToken cancellationToken);
    Task<Campaign> GetOwnedCampaignAsync(Guid campaignId, UserAccount currentUser, Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder, CancellationToken cancellationToken);
    Task<Campaign> GetOwnedOrClaimableCampaignAsync(Guid campaignId, UserAccount currentUser, Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder, CancellationToken cancellationToken);
    bool TryClaim(Campaign campaign, UserAccount currentUser, DateTime utcNow);
}

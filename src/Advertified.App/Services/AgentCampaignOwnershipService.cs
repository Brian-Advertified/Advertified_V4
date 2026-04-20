using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class AgentCampaignOwnershipService : IAgentCampaignOwnershipService
{
    private readonly AppDbContext _db;

    public AgentCampaignOwnershipService(AppDbContext db)
    {
        _db = db;
    }

    public IQueryable<Campaign> ApplyReadableScope(IQueryable<Campaign> query, UserAccount currentUser)
    {
        return currentUser.Role == UserRole.Agent
            ? query.Where(x => x.AssignedAgentUserId == currentUser.Id || x.AssignedAgentUserId == null)
            : query;
    }

    public IQueryable<Campaign> ApplyOwnedOrClaimableScope(IQueryable<Campaign> query, UserAccount currentUser)
    {
        return currentUser.Role == UserRole.Agent
            ? query.Where(x => x.AssignedAgentUserId == currentUser.Id || x.AssignedAgentUserId == null)
            : query;
    }

    public Task<Campaign> GetReadableCampaignAsync(
        Guid campaignId,
        UserAccount currentUser,
        Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder,
        CancellationToken cancellationToken)
    {
        return GetCampaignAsync(campaignId, currentUser, queryBuilder, allowClaimable: false, cancellationToken);
    }

    public Task<Campaign> GetOwnedCampaignAsync(
        Guid campaignId,
        UserAccount currentUser,
        Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder,
        CancellationToken cancellationToken)
    {
        return GetCampaignAsync(campaignId, currentUser, queryBuilder, allowClaimable: false, cancellationToken);
    }

    public Task<Campaign> GetOwnedOrClaimableCampaignAsync(
        Guid campaignId,
        UserAccount currentUser,
        Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder,
        CancellationToken cancellationToken)
    {
        return GetCampaignAsync(campaignId, currentUser, queryBuilder, allowClaimable: true, cancellationToken);
    }

    public bool TryClaim(Campaign campaign, UserAccount currentUser, DateTime utcNow)
    {
        if (currentUser.Role != UserRole.Agent || campaign.AssignedAgentUserId.HasValue)
        {
            return false;
        }

        campaign.AssignedAgentUserId = currentUser.Id;
        campaign.AssignedAt = utcNow;
        campaign.UpdatedAt = utcNow;
        return true;
    }

    private async Task<Campaign> GetCampaignAsync(
        Guid campaignId,
        UserAccount currentUser,
        Func<IQueryable<Campaign>, IQueryable<Campaign>>? queryBuilder,
        bool allowClaimable,
        CancellationToken cancellationToken)
    {
        IQueryable<Campaign> query = _db.Campaigns;
        if (queryBuilder is not null)
        {
            query = queryBuilder(query);
        }

        query = allowClaimable
            ? ApplyOwnedOrClaimableScope(query, currentUser)
            : ApplyReadableScope(query, currentUser);

        return await query.FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken)
            ?? throw new NotFoundException("Campaign not found.");
    }
}

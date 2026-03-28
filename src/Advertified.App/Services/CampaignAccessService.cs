using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CampaignAccessService : ICampaignAccessService
{
    private readonly AppDbContext _db;

    public CampaignAccessService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureCanCreateOrderAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (!user.EmailVerified)
        {
            throw new InvalidOperationException("Verify your email before buying a package.");
        }

        var identity = await _db.IdentityProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (identity is null)
        {
            throw new InvalidOperationException("Complete your identity details before buying a package.");
        }
    }

    public async Task EnsureCanEditBriefAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (campaign.PackageOrder.PaymentStatus != "paid")
        {
            throw new InvalidOperationException("This package has not been paid for yet.");
        }
    }

    public async Task EnsureCanGeneratePlanAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.PackageOrder)
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (campaign.PackageOrder.PaymentStatus != "paid")
        {
            throw new InvalidOperationException("You must pay for your package before planning.");
        }

        if (!campaign.AiUnlocked)
        {
            throw new InvalidOperationException("Submit your campaign brief before AI planning becomes available.");
        }

        if (campaign.Status is not ("brief_submitted" or "planning_in_progress"))
        {
            throw new InvalidOperationException("This campaign is not ready for planning.");
        }
    }
}

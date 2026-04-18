using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class ProspectLeadLinkingService : IProspectLeadLinkingService
{
    private readonly AppDbContext _db;

    public ProspectLeadLinkingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task ClaimByEmailAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var normalizedEmail = ProspectLeadContactNormalizer.NormalizeEmail(user.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var leadIds = await _db.ProspectLeads
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        if (leadIds.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var leads = await _db.ProspectLeads
            .Where(x => leadIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);
        foreach (var lead in leads)
        {
            lead.ClaimedUserId = user.Id;
            lead.Email = normalizedEmail;
            lead.NormalizedEmail = normalizedEmail;
            lead.UpdatedAt = now;
        }

        var campaigns = await _db.Campaigns
            .Where(x => x.UserId == null && x.ProspectLeadId.HasValue && leadIds.Contains(x.ProspectLeadId.Value))
            .ToArrayAsync(cancellationToken);
        foreach (var campaign in campaigns)
        {
            campaign.UserId = user.Id;
            campaign.UpdatedAt = now;
        }

        var orders = await _db.PackageOrders
            .Where(x => x.UserId == null && x.ProspectLeadId.HasValue && leadIds.Contains(x.ProspectLeadId.Value))
            .ToArrayAsync(cancellationToken);
        foreach (var order in orders)
        {
            order.UserId = user.Id;
            order.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

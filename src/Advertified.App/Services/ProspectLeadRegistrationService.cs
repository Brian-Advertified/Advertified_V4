using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class ProspectLeadRegistrationService : IProspectLeadRegistrationService
{
    private const string DuplicateMessage = "A prospect with this contact information already exists.";

    private readonly AppDbContext _db;

    public ProspectLeadRegistrationService(AppDbContext db)
    {
        _db = db;
    }

    public Task<ProspectLeadRegistrationResult> UpsertAgentLeadAsync(
        Guid agentUserId,
        string fullName,
        string email,
        string phone,
        string source,
        CancellationToken cancellationToken)
    {
        return UpsertAsync(fullName, email, phone, source, agentUserId, allowAnonymousReuse: false, cancellationToken);
    }

    public Task<ProspectLeadRegistrationResult> UpsertPublicLeadAsync(
        string fullName,
        string email,
        string phone,
        string source,
        CancellationToken cancellationToken)
    {
        return UpsertAsync(fullName, email, phone, source, ownerAgentUserId: null, allowAnonymousReuse: true, cancellationToken);
    }

    private async Task<ProspectLeadRegistrationResult> UpsertAsync(
        string fullName,
        string email,
        string phone,
        string source,
        Guid? ownerAgentUserId,
        bool allowAnonymousReuse,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = ProspectLeadContactNormalizer.NormalizeEmail(email);
        var normalizedPhone = ProspectLeadContactNormalizer.NormalizePhone(phone);
        var trimmedFullName = fullName.Trim();
        var trimmedPhone = phone.Trim();
        var now = DateTime.UtcNow;

        var lead = await _db.ProspectLeads
            .Include(x => x.ClaimedUser)
            .Include(x => x.Campaigns)
                .ThenInclude(x => x.PackageOrder)
            .FirstOrDefaultAsync(
                x => (!string.IsNullOrWhiteSpace(normalizedEmail) && x.NormalizedEmail == normalizedEmail)
                    || (!string.IsNullOrWhiteSpace(normalizedPhone) && x.NormalizedPhone == normalizedPhone),
                cancellationToken);

        if (lead is null && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            lead = await _db.ProspectLeads
                .Include(x => x.ClaimedUser)
                .Include(x => x.Campaigns)
                    .ThenInclude(x => x.PackageOrder)
                .FirstOrDefaultAsync(
                    x => x.ClaimedUser != null && x.ClaimedUser.Email == normalizedEmail,
                    cancellationToken);
        }

        var createdNewLead = false;
        if (lead is null)
        {
            lead = new ProspectLead
            {
                Id = Guid.NewGuid(),
                FullName = trimmedFullName,
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                Phone = trimmedPhone,
                NormalizedPhone = normalizedPhone,
                Source = source,
                OwnerAgentUserId = ownerAgentUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ProspectLeads.Add(lead);
            createdNewLead = true;
        }
        else
        {
            if (HasActiveProspectCampaign(lead))
            {
                if (!allowAnonymousReuse || lead.OwnerAgentUserId.HasValue)
                {
                    if (ownerAgentUserId is null || lead.OwnerAgentUserId != ownerAgentUserId)
                    {
                        throw new ConflictException(DuplicateMessage);
                    }

                    throw new ConflictException(DuplicateMessage);
                }
            }

            if (ownerAgentUserId.HasValue && lead.OwnerAgentUserId.HasValue && lead.OwnerAgentUserId != ownerAgentUserId)
            {
                throw new ConflictException(DuplicateMessage);
            }

            lead.FullName = trimmedFullName;
            lead.Email = normalizedEmail;
            lead.NormalizedEmail = normalizedEmail;
            lead.Phone = trimmedPhone;
            lead.NormalizedPhone = normalizedPhone;
            lead.Source = source;
            lead.OwnerAgentUserId ??= ownerAgentUserId;
            lead.UpdatedAt = now;
        }

        return new ProspectLeadRegistrationResult(lead, createdNewLead, normalizedEmail, normalizedPhone);
    }

    private static bool HasActiveProspectCampaign(ProspectLead lead)
    {
        return lead.Campaigns.Any(campaign =>
            ProspectCampaignPolicy.IsProspectiveCampaign(campaign)
            && !ProspectCampaignPolicy.IsClosed(campaign));
    }
}

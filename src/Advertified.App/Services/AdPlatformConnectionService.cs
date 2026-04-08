using Advertified.App.AIPlatform.Api;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class AdPlatformConnectionService : IAdPlatformConnectionService
{
    private readonly AppDbContext _db;

    public AdPlatformConnectionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CampaignAdPlatformConnectionResponse>> GetCampaignConnectionsAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.CampaignAdPlatformLinks
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .Include(item => item.AdPlatformConnection)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.AdPlatformConnection.Provider)
            .ThenBy(item => item.AdPlatformConnection.AccountName)
            .ToArrayAsync(cancellationToken);

        return rows.Select(MapRow).ToArray();
    }

    public async Task<CampaignAdPlatformConnectionResponse> UpsertCampaignConnectionAsync(
        Guid campaignId,
        Guid? ownerUserId,
        UpsertCampaignAdPlatformConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var provider = NormalizeProvider(request.Provider);
        var externalAccountId = (request.ExternalAccountId ?? string.Empty).Trim();
        var accountName = (request.AccountName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(externalAccountId) || string.IsNullOrWhiteSpace(accountName))
        {
            throw new InvalidOperationException("externalAccountId and accountName are required.");
        }

        var now = DateTime.UtcNow;
        var connection = await _db.AdPlatformConnections
            .FirstOrDefaultAsync(
                item => item.Provider == provider && item.ExternalAccountId == externalAccountId,
                cancellationToken);

        if (connection is null)
        {
            connection = new AdPlatformConnection
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Provider = provider,
                ExternalAccountId = externalAccountId,
                AccountName = accountName,
                Status = NormalizeStatus(request.Status),
                AccessToken = NormalizeOptionalText(request.AccessToken),
                RefreshToken = NormalizeOptionalText(request.RefreshToken),
                TokenExpiresAt = request.TokenExpiresAt?.UtcDateTime,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.AdPlatformConnections.Add(connection);
        }
        else
        {
            connection.OwnerUserId ??= ownerUserId;
            connection.AccountName = accountName;
            connection.Status = NormalizeStatus(request.Status);
            connection.AccessToken = NormalizeOptionalText(request.AccessToken) ?? connection.AccessToken;
            connection.RefreshToken = NormalizeOptionalText(request.RefreshToken) ?? connection.RefreshToken;
            connection.TokenExpiresAt = request.TokenExpiresAt?.UtcDateTime ?? connection.TokenExpiresAt;
            connection.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var link = await _db.CampaignAdPlatformLinks
            .FirstOrDefaultAsync(
                item => item.CampaignId == campaignId && item.AdPlatformConnectionId == connection.Id,
                cancellationToken);

        if (link is null)
        {
            link = new CampaignAdPlatformLink
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                AdPlatformConnectionId = connection.Id,
                ExternalCampaignId = NormalizeOptionalText(request.ExternalCampaignId),
                IsPrimary = request.IsPrimary,
                Status = NormalizeStatus(request.Status),
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.CampaignAdPlatformLinks.Add(link);
        }
        else
        {
            link.ExternalCampaignId = NormalizeOptionalText(request.ExternalCampaignId);
            link.IsPrimary = request.IsPrimary;
            link.Status = NormalizeStatus(request.Status);
            link.UpdatedAt = now;
        }

        if (request.IsPrimary)
        {
            var siblings = await _db.CampaignAdPlatformLinks
                .Where(item =>
                    item.CampaignId == campaignId
                    && item.Id != link.Id
                    && item.IsPrimary)
                .ToArrayAsync(cancellationToken);
            foreach (var sibling in siblings)
            {
                sibling.IsPrimary = false;
                sibling.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(link).Reference(item => item.AdPlatformConnection).LoadAsync(cancellationToken);
        return MapRow(link);
    }

    private static CampaignAdPlatformConnectionResponse MapRow(CampaignAdPlatformLink row)
    {
        return new CampaignAdPlatformConnectionResponse
        {
            LinkId = row.Id,
            ConnectionId = row.AdPlatformConnectionId,
            CampaignId = row.CampaignId,
            Provider = row.AdPlatformConnection.Provider,
            ExternalAccountId = row.AdPlatformConnection.ExternalAccountId,
            AccountName = row.AdPlatformConnection.AccountName,
            ExternalCampaignId = row.ExternalCampaignId,
            IsPrimary = row.IsPrimary,
            Status = row.Status,
            UpdatedAt = new DateTimeOffset(row.UpdatedAt, TimeSpan.Zero)
        };
    }

    private static string NormalizeProvider(string? value)
    {
        return AdPlatformProviderNormalizer.Normalize(value);
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "active" => "active",
            "paused" => "paused",
            "disconnected" => "disconnected",
            _ => "active"
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

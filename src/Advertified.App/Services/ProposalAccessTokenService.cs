using System.Text.Json;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Advertified.App.Services;

public sealed class ProposalAccessTokenService : IProposalAccessTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);
    private readonly IDataProtector _protector;

    public ProposalAccessTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Advertified.App.ProposalAccessToken.v1");
    }

    public string CreateToken(Guid campaignId)
    {
        var payload = new ProposalAccessTokenPayload(campaignId, DateTime.UtcNow);
        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    public bool TryReadToken(string token, out ProposalAccessTokenPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var json = _protector.Unprotect(token.Trim());
            var parsed = JsonSerializer.Deserialize<ProposalAccessTokenPayload>(json);
            if (parsed is null || parsed.CampaignId == Guid.Empty)
            {
                return false;
            }

            if (parsed.IssuedAtUtc < DateTime.UtcNow.Subtract(TokenLifetime))
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using System.Text.Json;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Advertified.App.Services;

public sealed class SessionTokenService : ISessionTokenService
{
    private readonly IDataProtector _protector;

    public SessionTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Advertified.App.SessionToken.v1");
    }

    public string CreateToken(UserAccount user)
    {
        var payload = new SessionTokenPayload(user.Id, user.PasswordHash, DateTime.UtcNow);
        var json = JsonSerializer.Serialize(payload);
        return _protector.Protect(json);
    }

    public bool TryReadToken(string token, out SessionTokenPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var json = _protector.Unprotect(token.Trim());
            var parsed = JsonSerializer.Deserialize<SessionTokenPayload>(json);
            if (parsed is null || parsed.UserId == Guid.Empty || string.IsNullOrWhiteSpace(parsed.PasswordHash))
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

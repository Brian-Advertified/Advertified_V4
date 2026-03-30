using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ISessionTokenService
{
    string CreateToken(UserAccount user);
    bool TryReadToken(string token, out SessionTokenPayload payload);
}

public sealed record SessionTokenPayload(Guid UserId, string PasswordHash, DateTime IssuedAtUtc);

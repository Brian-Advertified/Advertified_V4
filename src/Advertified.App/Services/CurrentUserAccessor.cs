using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;
    private readonly ISessionTokenService _sessionTokenService;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor, AppDbContext db, ISessionTokenService sessionTokenService)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
        _sessionTokenService = sessionTokenService;
    }

    public async Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var token = ReadBearerToken();
        if (string.IsNullOrWhiteSpace(token) || !_sessionTokenService.TryReadToken(token, out var payload))
        {
            throw new InvalidOperationException("A valid authenticated session is required.");
        }

        var user = await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payload.UserId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (!string.Equals(user.PasswordHash, payload.PasswordHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authenticated session is no longer valid.");
        }

        return user.Id;
    }

    private string? ReadBearerToken()
    {
        var authorizationHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string BearerPrefix = "Bearer ";
        return authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[BearerPrefix.Length..].Trim()
            : null;
    }
}

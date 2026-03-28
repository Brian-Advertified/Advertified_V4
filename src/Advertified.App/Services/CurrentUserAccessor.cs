using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private const string UserIdHeader = "X-User-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers[UserIdHeader].FirstOrDefault();
        if (Guid.TryParse(headerValue, out var headerUserId))
        {
            return headerUserId;
        }

        var latestUserId = await _db.UserAccounts
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUserId == Guid.Empty)
        {
            throw new InvalidOperationException("No user account is available. Register a user first or send X-User-Id.");
        }

        return latestUserId;
    }
}

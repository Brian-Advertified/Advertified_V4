using System.Security.Claims;
using System.Text.Encodings.Web;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Authentication;

public sealed class SessionTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ISessionTokenService _sessionTokenService;
    private readonly AppDbContext _db;

    #pragma warning disable CS0618
    public SessionTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        ISessionTokenService sessionTokenService,
        AppDbContext db)
        : base(options, logger, encoder, clock)
    {
        _sessionTokenService = sessionTokenService;
        _db = db;
    }
#pragma warning restore CS0618

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = TryReadBearerToken();
        token ??= TryReadSessionCookie();

        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        if (!_sessionTokenService.TryReadToken(token, out var payload))
        {
            return AuthenticateResult.Fail("Invalid or malformed session token.");
        }

        var user = await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payload.UserId, Context.RequestAborted);

        if (user is null)
        {
            return AuthenticateResult.Fail("Authenticated user account could not be found.");
        }

        if (!string.Equals(user.PasswordHash, payload.PasswordHash, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Authenticated session is no longer valid.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim("email", user.Email ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private string? TryReadBearerToken()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeaders))
        {
            return null;
        }

        var authorizationHeader = authorizationHeaders.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string BearerPrefix = "Bearer ";
        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorizationHeader[BearerPrefix.Length..].Trim();
    }

    private string? TryReadSessionCookie()
    {
        if (!Request.Cookies.TryGetValue(SessionCookieDefaults.CookieName, out var token))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }
}

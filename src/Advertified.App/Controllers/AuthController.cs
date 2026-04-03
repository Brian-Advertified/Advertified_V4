using Advertified.App.Contracts.Auth;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Advertified.App.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRegistrationService _registrationService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AuthController(
        AppDbContext db,
        IRegistrationService registrationService,
        IEmailVerificationService emailVerificationService,
        IPasswordHashingService passwordHashingService,
        ISessionTokenService sessionTokenService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _registrationService = registrationService;
        _emailVerificationService = emailVerificationService;
        _passwordHashingService = passwordHashingService;
        _sessionTokenService = sessionTokenService;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registrationService.RegisterAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<LoginResponse>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await _emailVerificationService.VerifyAsync(request.Token, cancellationToken);

        var identityComplete = await _db.IdentityProfiles
            .AsNoTracking()
            .AnyAsync(x => x.UserId == user.Id, cancellationToken);

        return Ok(ToLoginResponse(user, _sessionTokenService.CreateToken(user), identityComplete));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || !_passwordHashingService.VerifyPassword(user, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid log in, please check your email and password.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!user.EmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Your account has not been activated yet. Please check your email for the activation link.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        var identityComplete = await _db.IdentityProfiles
            .AsNoTracking()
            .AnyAsync(x => x.UserId == user.Id, cancellationToken);

        return Ok(ToLoginResponse(user, _sessionTokenService.CreateToken(user), identityComplete));
    }

    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await _emailVerificationService.ResendActivationAsync(request.Email, request.NextPath, cancellationToken);

        return Accepted(new
        {
            Message = "Verification link resent.",
            request.Email
        });
    }

    [HttpPost("set-password")]
    public async Task<ActionResult<LoginResponse>> SetPassword([FromBody] SetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!RegistrationValidators.IsStrongPassword(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password must be at least 12 characters and include uppercase, lowercase, number, and symbol.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!string.Equals(request.Password?.Trim(), request.ConfirmPassword?.Trim(), StringComparison.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password confirmation does not match.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User account not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var normalizedPassword = (request.Password ?? string.Empty).Trim();
        user.PasswordHash = _passwordHashingService.HashPassword(user, normalizedPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var identityComplete = await _db.IdentityProfiles
            .AsNoTracking()
            .AnyAsync(x => x.UserId == user.Id, cancellationToken);

        return Ok(ToLoginResponse(user, _sessionTokenService.CreateToken(user), identityComplete));
    }

    private static LoginResponse ToLoginResponse(UserAccount user, string sessionToken, bool identityComplete)
    {
        return new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = ToSnakeCase(user.Role.ToString()),
            AccountStatus = ToSnakeCase(user.AccountStatus.ToString()),
            EmailVerified = user.EmailVerified,
            IdentityComplete = identityComplete,
            SessionToken = sessionToken
        };
    }

    private static string ToSnakeCase(string value)
    {
        return Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }
}

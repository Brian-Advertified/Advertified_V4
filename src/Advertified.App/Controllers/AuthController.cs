using Advertified.App.Contracts.Auth;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Advertified.App.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRegistrationService _registrationService;
    private readonly IEmailVerificationService _emailVerificationService;

    public AuthController(
        AppDbContext db,
        IRegistrationService registrationService,
        IEmailVerificationService emailVerificationService)
    {
        _db = db;
        _registrationService = registrationService;
        _emailVerificationService = emailVerificationService;
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

        return Ok(ToLoginResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var passwordHash = HashPassword(request.Password);

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail && x.PasswordHash == passwordHash, cancellationToken);

        if (user is null)
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

        return Ok(ToLoginResponse(user));
    }

    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await _emailVerificationService.ResendActivationAsync(request.Email, cancellationToken);

        return Accepted(new
        {
            Message = "Verification link resent.",
            request.Email
        });
    }

    private static LoginResponse ToLoginResponse(Data.Entities.UserAccount user)
    {
        return new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = ToSnakeCase(user.Role.ToString()),
            AccountStatus = ToSnakeCase(user.AccountStatus.ToString()),
            EmailVerified = user.EmailVerified
        };
    }

    private static string ToSnakeCase(string value)
    {
        return Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}

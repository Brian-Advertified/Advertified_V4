using System.Security.Cryptography;
using System.Text;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class EmailVerificationService : IEmailVerificationService
{
    private const int VerificationExpiryHours = 24;

    private readonly AppDbContext _db;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        AppDbContext db,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<EmailVerificationService> logger)
    {
        _db = db;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task QueueActivationEmailAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var nowUtc = DateTime.UtcNow;

        _db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = nowUtc.AddHours(VerificationExpiryHours),
            CreatedAt = nowUtc
        });

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendAsync(
                "user_activation",
                user.Email,
                "noreply",
                new Dictionary<string, string?>
                {
                    ["UserName"] = user.FullName,
                    ["ExpiresInHours"] = VerificationExpiryHours.ToString(),
                    ["ActivationUrl"] = BuildFrontendUrl($"/verify-email?token={Uri.EscapeDataString(rawToken)}")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send activation email for user {UserId}.", user.Id);
        }
    }

    public async Task<UserAccount> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var nowUtc = DateTime.UtcNow;

        var verificationToken = await _db.EmailVerificationTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash &&
                     x.UsedAt == null &&
                     x.ExpiresAt > nowUtc,
                cancellationToken)
            ?? throw new InvalidOperationException("Invalid or expired verification token.");

        verificationToken.UsedAt = nowUtc;
        verificationToken.User.EmailVerified = true;
        verificationToken.User.AccountStatus = AccountStatus.Active;
        verificationToken.User.UpdatedAt = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendAsync(
                "welcome",
                verificationToken.User.Email,
                "noreply",
                new Dictionary<string, string?>
                {
                    ["UserName"] = verificationToken.User.FullName,
                    ["SignInUrl"] = BuildFrontendUrl("/login")
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email for user {UserId}.", verificationToken.User.Id);
        }

        return verificationToken.User;
    }

    public async Task ResendActivationAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken)
            ?? throw new InvalidOperationException("User account not found.");

        if (user.EmailVerified)
        {
            return;
        }

        await QueueActivationEmailAsync(user, cancellationToken);
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes);
    }
}

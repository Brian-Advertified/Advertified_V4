using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Advertified.App.Services;

public sealed class PasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();

    public string HashPassword(UserAccount user, string password)
    {
        var normalizedPassword = password.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPassword))
        {
            throw new InvalidOperationException("Password is required.");
        }

        return _passwordHasher.HashPassword(user, normalizedPassword);
    }

    public bool VerifyPassword(UserAccount user, string password)
    {
        var normalizedPassword = password.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPassword) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, normalizedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

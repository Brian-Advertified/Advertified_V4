using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Advertified.App.Services;

public sealed class AdPlatformTokenCipher : IAdPlatformTokenCipher
{
    private const string EncryptedPrefix = "enc:";
    private readonly IDataProtector _protector;

    public AdPlatformTokenCipher(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Advertified.AdPlatformTokens.v1");
    }

    public string? Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            return trimmed;
        }

        return EncryptedPrefix + _protector.Protect(trimmed);
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            // Backward compatibility for legacy plain text tokens.
            return trimmed;
        }

        var payload = trimmed[EncryptedPrefix.Length..];
        try
        {
            return _protector.Unprotect(payload);
        }
        catch
        {
            return null;
        }
    }
}

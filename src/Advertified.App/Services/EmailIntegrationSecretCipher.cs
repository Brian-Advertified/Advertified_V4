using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Advertified.App.Services;

public sealed class EmailIntegrationSecretCipher : IEmailIntegrationSecretCipher
{
    private const string EncryptedPrefix = "enc:";
    private readonly IDataProtector _protector;

    public EmailIntegrationSecretCipher(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Advertified.EmailIntegrationSecrets.v1");
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
            return trimmed;
        }

        try
        {
            return _protector.Unprotect(trimmed[EncryptedPrefix.Length..]);
        }
        catch
        {
            return null;
        }
    }
}

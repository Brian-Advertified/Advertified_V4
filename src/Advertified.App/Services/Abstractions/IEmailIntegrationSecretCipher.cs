namespace Advertified.App.Services.Abstractions;

public interface IEmailIntegrationSecretCipher
{
    string? Protect(string? value);
    string? Unprotect(string? value);
}

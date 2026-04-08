namespace Advertified.App.Services.Abstractions;

public interface IAdPlatformTokenCipher
{
    string? Protect(string? value);
    string? Unprotect(string? value);
}

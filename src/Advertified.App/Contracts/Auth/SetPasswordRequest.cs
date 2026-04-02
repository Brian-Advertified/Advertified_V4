namespace Advertified.App.Contracts.Auth;

public sealed class SetPasswordRequest
{
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

namespace Advertified.App.Contracts.Auth;

public sealed class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerificationRequired { get; set; } = true;
    public string AccountStatus { get; set; } = "pending_verification";
}

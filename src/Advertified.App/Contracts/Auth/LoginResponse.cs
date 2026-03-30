namespace Advertified.App.Contracts.Auth;

public sealed class LoginResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string SessionToken { get; set; } = string.Empty;
}

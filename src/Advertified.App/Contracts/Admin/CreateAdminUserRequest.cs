namespace Advertified.App.Contracts.Admin;

public sealed class CreateAdminUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public bool IsSaCitizen { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
}

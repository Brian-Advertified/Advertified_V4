namespace Advertified.App.Contracts.Users;

public sealed class MeResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool IdentityComplete { get; set; }
    public bool PhoneVerified { get; set; }
    public string? BusinessName { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
}

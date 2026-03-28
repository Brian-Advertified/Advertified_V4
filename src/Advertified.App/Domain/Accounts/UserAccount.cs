namespace Advertified.App.Domain.Accounts;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "client";
    public string AccountStatus { get; set; } = "pending_verification";
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsSaCitizen { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public IdentityProfile? IdentityProfile { get; set; }
}

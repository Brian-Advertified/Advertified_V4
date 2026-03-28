namespace Advertified.App.Domain.Accounts;

public sealed class IdentityProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IdentityType { get; set; } = string.Empty;
    public string? SaIdNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? PassportCountryIso2 { get; set; }
    public DateOnly? PassportIssueDate { get; set; }
    public DateOnly? PassportValidUntil { get; set; }
    public string VerificationStatus { get; set; } = "not_submitted";
}

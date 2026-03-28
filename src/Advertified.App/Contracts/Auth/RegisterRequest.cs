using System.ComponentModel.DataAnnotations;

namespace Advertified.App.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public bool IsSouthAfricanCitizen { get; set; }

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string BusinessName { get; set; } = string.Empty;

    [Required]
    public string BusinessType { get; set; } = string.Empty;

    [Required]
    public string RegistrationNumber { get; set; } = string.Empty;

    public string? VatNumber { get; set; }

    [Required]
    public string Industry { get; set; } = string.Empty;

    [Required]
    public string AnnualRevenueBand { get; set; } = string.Empty;

    public string? TradingAsName { get; set; }

    [Required]
    public string StreetAddress { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string Province { get; set; } = string.Empty;

    public string? SaIdNumber { get; set; }

    public string? PassportNumber { get; set; }

    public string? PassportCountryIso2 { get; set; }

    public DateOnly? PassportIssueDate { get; set; }

    public DateOnly? PassportValidUntil { get; set; }
}

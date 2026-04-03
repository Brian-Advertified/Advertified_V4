using System.ComponentModel.DataAnnotations;

namespace Advertified.App.Contracts.Auth;

public sealed class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? NextPath { get; set; }
}

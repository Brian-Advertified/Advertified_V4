using System.ComponentModel.DataAnnotations;

namespace Advertified.App.Contracts.Auth;

public sealed class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

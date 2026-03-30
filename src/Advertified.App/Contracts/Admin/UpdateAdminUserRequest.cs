using System;
using System.Collections.Generic;

namespace Advertified.App.Contracts.Admin;

public sealed class UpdateAdminUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public bool IsSaCitizen { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public IReadOnlyList<string> AssignedAreaCodes { get; set; } = Array.Empty<string>();
}

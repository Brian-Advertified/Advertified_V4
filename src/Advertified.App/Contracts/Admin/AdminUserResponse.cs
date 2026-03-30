using System;
using System.Collections.Generic;

namespace Advertified.App.Contracts.Admin;

public sealed class AdminUserResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public bool IsSaCitizen { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public IReadOnlyList<string> AssignedAreaCodes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AssignedAreaLabels { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class IdentityProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? SaIdNumber { get; set; }

    public string? PassportNumber { get; set; }

    public string? PassportCountryIso2 { get; set; }

    public DateOnly? PassportIssueDate { get; set; }

    public DateOnly? PassportValidUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}

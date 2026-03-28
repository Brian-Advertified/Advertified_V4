using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class UserAccount
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsSaCitizen { get; set; }

    public bool EmailVerified { get; set; }

    public bool PhoneVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual BusinessProfile? BusinessProfile { get; set; }

    public virtual ICollection<CampaignRecommendation> CampaignRecommendations { get; set; } = new List<CampaignRecommendation>();

    public virtual ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();

    public virtual ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();

    public virtual IdentityProfile? IdentityProfile { get; set; }

    public virtual ICollection<PackageOrder> PackageOrders { get; set; } = new List<PackageOrder>();
}

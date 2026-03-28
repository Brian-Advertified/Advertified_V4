using Advertified.App.Data.Enums;

namespace Advertified.App.Data.Entities;

public partial class IdentityProfile
{
    public IdentityType IdentityType { get; set; }

    public VerificationStatus VerificationStatus { get; set; }
}

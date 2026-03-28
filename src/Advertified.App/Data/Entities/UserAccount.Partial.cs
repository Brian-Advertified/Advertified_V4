using Advertified.App.Data.Enums;

namespace Advertified.App.Data.Entities;

public partial class UserAccount
{
    public UserRole Role { get; set; }

    public AccountStatus AccountStatus { get; set; }
}

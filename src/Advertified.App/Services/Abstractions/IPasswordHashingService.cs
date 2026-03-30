using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IPasswordHashingService
{
    string HashPassword(UserAccount user, string password);
    bool VerifyPassword(UserAccount user, string password);
}

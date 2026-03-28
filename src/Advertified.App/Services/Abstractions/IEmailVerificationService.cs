using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IEmailVerificationService
{
    Task QueueActivationEmailAsync(UserAccount user, CancellationToken cancellationToken);

    Task<Data.Entities.UserAccount> VerifyAsync(string token, CancellationToken cancellationToken);

    Task ResendActivationAsync(string email, CancellationToken cancellationToken);
}

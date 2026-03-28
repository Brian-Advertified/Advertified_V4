using Advertified.App.Contracts.Auth;

namespace Advertified.App.Services.Abstractions;

public interface IRegistrationService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}

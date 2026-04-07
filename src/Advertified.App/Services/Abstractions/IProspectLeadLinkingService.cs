using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IProspectLeadLinkingService
{
    Task ClaimByEmailAsync(UserAccount user, CancellationToken cancellationToken);
}

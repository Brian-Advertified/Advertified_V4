using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;

namespace Advertified.App.Services.Abstractions;

public interface IProspectDispositionService
{
    Task CloseAsync(Campaign campaign, Guid actorUserId, UserRole actorRole, string reasonCode, string? notes, CancellationToken cancellationToken);
    Task ReopenAsync(Campaign campaign, Guid actorUserId, UserRole actorRole, CancellationToken cancellationToken);
}

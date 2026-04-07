using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ISignalCollectorService
{
    Task<Signal> CollectAsync(int leadId, CancellationToken cancellationToken);
}

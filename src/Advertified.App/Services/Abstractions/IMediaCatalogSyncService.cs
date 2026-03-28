namespace Advertified.App.Services.Abstractions;

public interface IMediaCatalogSyncService
{
    Task SyncAsync(CancellationToken cancellationToken);
}
